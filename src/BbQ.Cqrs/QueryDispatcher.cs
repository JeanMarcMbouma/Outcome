// The query dispatcher implementation that coordinates the CQRS pipeline for queries
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace BbQ.Cqrs;

/// <summary>
/// The concrete implementation of IQueryDispatcher for the CQRS pattern.
/// 
/// This dispatcher:
/// 1. Resolves the handler for a given query
/// 2. Builds a pipeline of behaviors in registration order
/// 3. Executes the pipeline with the handler as the terminal
/// 4. Returns the response from the handler
/// 
/// This implementation uses reflection during the first dispatch of each
/// query/response type pair to build and cache the pipeline. Subsequent
/// dispatches reuse the cached pipeline without additional reflection
/// overhead, while handlers and behaviors are still resolved via
/// dependency injection.
/// </summary>
/// <remarks>
/// The dispatcher uses dependency injection to resolve:
/// - The specific IRequestHandler&lt;TQuery, TResponse&gt; implementation
/// - All registered IPipelineBehavior&lt;TQuery, TResponse&gt; implementations
/// 
/// Pipeline construction:
/// - Behaviors are retrieved from the service provider in registration order
/// - They are then composed in reverse order to form the chain
/// - This ensures the first registered behavior is the outermost
/// - The handler becomes the innermost (terminal) of the pipeline
/// 
/// Example pipeline for 2 behaviors:
/// <code>
/// query
///   -> Behavior1.Handle()
///        -> Behavior2.Handle()
///             -> Handler.Handle()
/// </code>
/// </remarks>
internal sealed class QueryDispatcher(IServiceProvider sp) : IQueryDispatcher
{
    private readonly IServiceProvider _sp = sp;

    private readonly ConcurrentDictionary<(Type Qry, Type Res),
        Func<object, CancellationToken, Task>> _dispatchCache = new();

    private readonly ConcurrentDictionary<(Type Qry, Type Item),
        Func<object, CancellationToken, IAsyncEnumerable<object>>> _streamCache = new();

    /// <summary>
    /// Dispatches a query through the CQRS pipeline and returns a response.
    /// </summary>
    /// <typeparam name="TResponse">The response type</typeparam>
    /// <param name="query">The query to dispatch</param>
    /// <param name="ct">Cancellation token for async operations</param>
    /// <returns>The response from the handler after passing through all behaviors</returns>
    /// <remarks>
    /// Process:
    /// 1. Resolves the handler with GetRequiredService()
    /// 2. Resolves all behaviors with GetServices()
    /// 3. Composes behaviors in reverse order
    /// 4. Invokes the composed pipeline with the query
    /// 5. Returns the final response
    /// 
    /// If no handler is registered, GetRequiredService() throws InvalidOperationException.
    /// If no behaviors are registered, the query goes directly to the handler.
    /// </remarks>
    public async Task<TResponse> Dispatch<TResponse>(IQuery<TResponse> query, CancellationToken ct = default)
    {
        // Resolve strongly-typed handler - throws if not registered
        var key = (query.GetType(), typeof(TResponse));

        var dispatcher = _dispatchCache.GetOrAdd(key, k =>
        {
            var (qryType, resType) = k;

            // Resolve handler
            var handlerType = typeof(IRequestHandler<,>).MakeGenericType(qryType, resType);
            var handleMethod = handlerType.GetMethod("Handle")!;

            Task<TResponse> terminal(object qry, CancellationToken token)
            {
                var handler = _sp.GetRequiredService(handlerType);
                return (Task<TResponse>)handleMethod.Invoke(handler, [qry, token])!;
            }

            // Resolve behaviors in registration order (first registered becomes outermost)
            // This creates FIFO order before handler, LIFO order after handler
            var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(qryType, resType);
            var behaviors = _sp.GetServices(behaviorType).Reverse().ToArray();

            Func<object, CancellationToken, Task<TResponse>> pipeline = terminal;
            foreach (var b in behaviors)
            {
                var method = behaviorType.GetMethod("Handle")!;
                var next = pipeline;
                pipeline = (qry, token) =>
                    (Task<TResponse>)method.Invoke(b,
                    [
                            qry,
                            token,
                            new Func<object, CancellationToken, Task<TResponse>>(next)
                    ])!;
            }

            return pipeline;
        });

        return await (Task<TResponse>)dispatcher(query, ct);
    }

    /// <summary>
    /// Dispatches a streaming query through the CQRS pipeline and returns a stream of items.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the stream</typeparam>
    /// <param name="query">The streaming query to dispatch</param>
    /// <param name="ct">Cancellation token for async operations</param>
    /// <returns>An asynchronous stream of items from the handler</returns>
    /// <remarks>
    /// Process:
    /// 1. Resolves the stream handler metadata and behaviors (cached per type)
    /// 2. Handler instances are resolved per-call to support scoped lifetimes
    /// 3. Composes behaviors in reverse order
    /// 4. Invokes the composed pipeline with the query
    /// 5. Returns the stream
    /// 
    /// If no handler is registered, GetRequiredService() throws InvalidOperationException.
    /// If no behaviors are registered, the query goes directly to the handler.
    /// 
    /// The pipeline construction is cached per query/item type pair to avoid repeated reflection overhead.
    /// </remarks>
    public async IAsyncEnumerable<TItem> Stream<TItem>(
        IStreamQuery<TItem> query, 
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var key = (query.GetType(), typeof(TItem));

        var dispatcher = _streamCache.GetOrAdd(key, k =>
        {
            var (qryType, itemType) = k;

            // Resolve handler metadata (type and method). Handler instances are resolved per-call in the terminal.
            var handlerType = typeof(IStreamHandler<,>).MakeGenericType(qryType, itemType);
            var handleMethod = handlerType.GetMethod("Handle")!;

            async IAsyncEnumerable<object> terminal(object qry, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
            {
                var handler = _sp.GetRequiredService(handlerType);
                var stream = (IAsyncEnumerable<TItem>)handleMethod.Invoke(handler, [qry, token])!;
                await foreach (var item in stream.ConfigureAwait(false))
                {
                    yield return item!;
                }
            }

            // Resolve behaviors in registration order (first registered becomes outermost)
            var behaviorType = typeof(IStreamPipelineBehavior<,>).MakeGenericType(qryType, itemType);
            var behaviors = _sp.GetServices(behaviorType).Reverse().ToArray();

            Func<object, CancellationToken, IAsyncEnumerable<object>> pipeline = terminal;
            foreach (var b in behaviors)
            {
                var method = behaviorType.GetMethod("Handle")!;
                var next = pipeline;
                pipeline = (qry, token) =>
                {
                    var typedNext = CreateTypedNextFunc<TItem>(next);
                    var result = method.Invoke(b, [qry, token, typedNext]);
                    return ConvertToObjectStream((IAsyncEnumerable<TItem>)result!);
                };
            }

            return pipeline;
        });

        await foreach (var item in dispatcher(query, ct).ConfigureAwait(false))
        {
            yield return (TItem)item;
        }
    }

    private static Func<object, CancellationToken, IAsyncEnumerable<TItem>> CreateTypedNextFunc<TItem>(
        Func<object, CancellationToken, IAsyncEnumerable<object>> next)
    {
        return (qry, token) => ConvertFromObjectStream<TItem>(next(qry, token));
    }

    private static async IAsyncEnumerable<object> ConvertToObjectStream<TItem>(IAsyncEnumerable<TItem> source)
    {
        await foreach (var item in source.ConfigureAwait(false))
        {
            yield return item!;
        }
    }

    private static async IAsyncEnumerable<TItem> ConvertFromObjectStream<TItem>(IAsyncEnumerable<object> source)
    {
        await foreach (var item in source.ConfigureAwait(false))
        {
            yield return (TItem)item;
        }
    }
}
