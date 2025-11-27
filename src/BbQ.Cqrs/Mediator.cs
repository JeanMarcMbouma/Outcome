// The core mediator implementation that coordinates the CQRS pipeline
using BbQ.Cqrs;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace BbQ.Cqrs;

/// <summary>
/// The concrete implementation of IMediator for the CQRS pattern.
/// 
/// This mediator:
/// 1. Resolves the handler for a given request
/// 2. Builds a pipeline of behaviors in registration order
/// 3. Executes the pipeline with the handler as the terminal
/// 4. Returns the response from the handler
/// </summary>
/// <remarks>
/// The mediator uses dependency injection to resolve:
/// - The specific IRequestHandler&lt;TRequest, TResponse&gt; implementation
/// - All registered IPipelineBehavior&lt;TRequest, TResponse&gt; implementations
/// 
/// Pipeline construction:
/// - Behaviors are retrieved from the service provider in registration order
/// - They are then composed in reverse order to form the chain
/// - This ensures the first registered behavior is the outermost
/// - The handler becomes the innermost (terminal) of the pipeline
/// 
/// Example pipeline for 2 behaviors:
/// <code>
/// request
///   -> Behavior1.Handle()
///        -> Behavior2.Handle()
///             -> Handler.Handle()
/// </code>
/// </remarks>
internal sealed class Mediator(IServiceProvider sp) : IMediator
{
    private readonly IServiceProvider _sp = sp;

    private readonly ConcurrentDictionary<(Type Req, Type Res),
        Func<object, CancellationToken, Task>> _dispatchCache = new();


    /// <summary>
    /// Sends a request through the CQRS pipeline and returns a response.
    /// </summary>
    /// <typeparam name="TRequest">The request type</typeparam>
    /// <typeparam name="TResponse">The response type</typeparam>
    /// <param name="request">The request to send</param>
    /// <param name="ct">Cancellation token for async operations</param>
    /// <returns>The response from the handler after passing through all behaviors</returns>
    /// <remarks>
    /// Process:
    /// 1. Resolves the handler with GetRequiredService()
    /// 2. Resolves all behaviors with GetServices()
    /// 3. Composes behaviors in reverse order
    /// 4. Invokes the composed pipeline with the request
    /// 5. Returns the final response
    /// 
    /// If no handler is registered, GetRequiredService() throws InvalidOperationException.
    /// If no behaviors are registered, the request goes directly to the handler.
    /// </remarks>
    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
    {
        // Resolve strongly-typed handler - throws if not registered
        var key = (request.GetType(), typeof(TResponse));

        var dispatcher = _dispatchCache.GetOrAdd(key, k =>
        {
            var (reqType, resType) = k;

            // Resolve handler
            var handlerType = typeof(IRequestHandler<,>).MakeGenericType(reqType, resType);
            var handleMethod = handlerType.GetMethod("Handle")!;

            Task<TResponse> terminal(object req, CancellationToken token)
            {
                var handler = _sp.GetRequiredService(handlerType);
                return (Task<TResponse>)handleMethod.Invoke(handler, [req, token])!;
            }

            // Resolve behaviors (outermost first, wrap inner)
            var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(reqType, resType);
            var behaviors = _sp.GetServices(behaviorType).Reverse().ToArray();

            Func<object, CancellationToken, Task<TResponse>> pipeline = terminal;
            foreach (var b in behaviors)
            {
                var method = behaviorType.GetMethod("Handle")!;
                var next = pipeline;
                pipeline = (req, token) =>
                    (Task<TResponse>)method.Invoke(b,
                    [
                            req,
                            token,
                            new Func<object, CancellationToken, Task<TResponse>>(next)
                    ])!;
            }

            return pipeline;
        });

        var resultObj = await (Task<TResponse>)dispatcher(request!, ct);
        return resultObj;

    }

    /// <summary>
    /// Sends a fire-and-forget request (void-like) through the CQRS pipeline.
    /// </summary>
    /// <typeparam name="TRequest">The request type</typeparam>
    /// <param name="request">The request to send</param>
    /// <param name="ct">Cancellation token for async operations</param>
    /// <returns>A task that completes when the handler finishes executing</returns>
    /// <remarks>
    /// Process:
    /// 1. Resolves the handler implementing IRequestHandler&lt;TRequest&gt; (single type parameter)
    /// 2. Resolves all behaviors as IPipelineBehavior&lt;TRequest, Unit&gt; (Unit as response type)
    /// 3. Composes behaviors in reverse order with a Task-returning terminal
    /// 4. Invokes the composed pipeline with the request
    /// 5. Returns the task without unwrapping any response value
    /// 
    /// Important: The handler is resolved as IRequestHandler&lt;TRequest&gt; (fire-and-forget style),
    /// but behaviors are resolved as IPipelineBehavior&lt;TRequest, Unit&gt; to work with the
    /// fully generic pipeline infrastructure. The Unit type parameter in behaviors is for framework
    /// consistency but is not exposed to handlers, which have no return type.
    /// 
    /// This overload is useful for operations that don't need to return a value,
    /// such as sending emails, publishing events, or executing background jobs.
    /// If no handler is registered, GetRequiredService() throws InvalidOperationException.
    /// </remarks>
    public Task Send(IRequest request, CancellationToken ct = default)
    {
        // Resolve strongly-typed handler - throws if not registered
        var key = (request.GetType(), typeof(Unit));

        var dispatcher = _dispatchCache.GetOrAdd(key, k =>
        {
            var (reqType, resType) = k;

            // Resolve handler
            var handlerType = typeof(IRequestHandler<>).MakeGenericType(reqType);
            var handleMethod = handlerType.GetMethod("Handle")!;

            Task<Unit> terminal(object req, CancellationToken token)
            {
                var handler = _sp.GetRequiredService(handlerType);
                handleMethod.Invoke(handler, [req, token]);
                return Task.FromResult(Unit.Value);
            }

            // Resolve behaviors (outermost first, wrap inner)
            var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(reqType, resType);
            var behaviors = _sp.GetServices(behaviorType).Reverse().ToArray();

            Func<object, CancellationToken, Task<Unit>> pipeline = terminal;
            foreach (var b in behaviors)
            {
                var method = behaviorType.GetMethod("Handle")!;
                var next = pipeline;
                pipeline = (req, token) =>
                    (Task<Unit>)method.Invoke(b,
                    [
                            req,
                            token,
                            new Func<object, CancellationToken, Task<Unit>>(next)
                    ])!;
            }

            return pipeline;
        });

        return dispatcher(request!, ct);
    }
}
