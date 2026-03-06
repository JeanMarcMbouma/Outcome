// The query dispatcher implementation that coordinates the CQRS pipeline for queries
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Reflection;

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
        object> _streamCache = new();

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
    public Task<TResponse> Dispatch<TResponse>(IQuery<TResponse> query, CancellationToken ct = default)
    {
        // Resolve strongly-typed handler - throws if not registered
        var key = (query.GetType(), typeof(TResponse));

        var dispatcher = _dispatchCache.GetOrAdd(key, k =>
        {
            var (qryType, resType) = k;

            var factoryMethod = typeof(QueryDispatcher)
                .GetMethod(nameof(CreateDispatcherCore), BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(qryType, resType);

            return (Func<object, CancellationToken, Task>)factoryMethod.Invoke(this, null)!;
        });

        return (Task<TResponse>)dispatcher(query, ct);
    }

    private Func<object, CancellationToken, Task> CreateDispatcherCore<TQuery, TResponse>()
        where TQuery : IQuery<TResponse>
    {
        Task<TResponse> Terminal(TQuery qry, CancellationToken token)
        {
            var handler = _sp.GetRequiredService<IRequestHandler<TQuery, TResponse>>();
            return handler.Handle(qry, token);
        }

        var behaviors = _sp
            .GetServices<IPipelineBehavior<TQuery, TResponse>>()
            .Reverse()
            .ToArray();

        Func<TQuery, CancellationToken, Task<TResponse>> pipeline = Terminal;
        foreach (var behavior in behaviors)
        {
            var next = pipeline;
            pipeline = (qry, token) => behavior.Handle(qry, token, next);
        }

        return (qry, token) => pipeline((TQuery)qry, token);
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
    public IAsyncEnumerable<TItem> Stream<TItem>(
        IStreamQuery<TItem> query,
        CancellationToken ct = default)
    {
        var key = (query.GetType(), typeof(TItem));

        var dispatcher = (Func<object, CancellationToken, IAsyncEnumerable<TItem>>)_streamCache.GetOrAdd(key, k =>
        {
            var (qryType, itemType) = k;

            var factoryMethod = typeof(QueryDispatcher)
                .GetMethod(nameof(CreateStreamDispatcherCore), BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(qryType, itemType);

            return factoryMethod.Invoke(this, null)!;
        });

        return dispatcher(query, ct);
    }

    private Func<object, CancellationToken, IAsyncEnumerable<TItem>> CreateStreamDispatcherCore<TQuery, TItem>()
        where TQuery : IStreamQuery<TItem>
    {
        IAsyncEnumerable<TItem> Terminal(TQuery qry, CancellationToken token)
        {
            var handler = _sp.GetRequiredService<IStreamHandler<TQuery, TItem>>();
            return handler.Handle(qry, token);
        }

        var behaviors = _sp
            .GetServices<IStreamPipelineBehavior<TQuery, TItem>>()
            .Reverse()
            .ToArray();

        Func<TQuery, CancellationToken, IAsyncEnumerable<TItem>> pipeline = Terminal;
        foreach (var behavior in behaviors)
        {
            var next = pipeline;
            pipeline = (qry, token) => behavior.Handle(qry, token, next);
        }

        return (qry, token) => pipeline((TQuery)qry, token);
    }
}
