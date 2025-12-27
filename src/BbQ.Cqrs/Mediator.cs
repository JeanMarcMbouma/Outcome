// The core mediator implementation that coordinates the CQRS pipeline
using BbQ.Cqrs;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace BbQ.Cqrs;

/// <summary>
/// The concrete implementation of IMediator for the CQRS pattern.
/// 
/// This mediator delegates to specialized dispatchers:
/// - Commands are routed to ICommandDispatcher
/// - Queries are routed to IQueryDispatcher
/// 
/// This design allows the mediator to be a thin facade over the dispatchers,
/// providing a unified interface while maintaining separation of concerns.
/// </summary>
/// <remarks>
/// The mediator uses the following dispatchers:
/// - ICommandDispatcher for ICommand&lt;TResponse&gt; requests
/// - IQueryDispatcher for IQuery&lt;TResponse&gt; requests
/// 
/// This approach:
/// - Eliminates code duplication between Mediator and dispatchers
/// - Allows dispatchers to be used independently or through the mediator
/// - Maintains interchangeability between IMediator and the specialized dispatchers
/// - Provides optimization opportunities for source generators
/// 
/// Example usage:
/// <code>
/// // Using mediator (unified interface)
/// var result = await mediator.Send(new CreateUserCommand());
/// 
/// // Using dispatcher directly (explicit command/query separation)
/// var result = await commandDispatcher.Dispatch(new CreateUserCommand());
/// </code>
/// </remarks>
internal sealed class Mediator(IServiceProvider sp) : IMediator
{
    private readonly IServiceProvider _sp = sp;
    private readonly ICommandDispatcher _commandDispatcher = sp.GetRequiredService<ICommandDispatcher>();
    private readonly IQueryDispatcher _queryDispatcher = sp.GetRequiredService<IQueryDispatcher>();

    private readonly ConcurrentDictionary<(Type Req, Type Res),
        Func<object, CancellationToken, Task>> _fireAndForgetCache = new();
    
    private readonly ConcurrentDictionary<(Type Req, Type Res),
        Func<object, CancellationToken, Task>> _genericRequestCache = new();


    /// <summary>
    /// Sends a request through the CQRS pipeline and returns a response.
    /// </summary>
    /// <typeparam name="TResponse">The response type returned by the handler</typeparam>
    /// <param name="request">The request to send (must be ICommand&lt;TResponse&gt;, IQuery&lt;TResponse&gt;, or IRequest&lt;TResponse&gt;)</param>
    /// <param name="ct">Cancellation token for async operations</param>
    /// <returns>The response from the handler after passing through all behaviors</returns>
    /// <remarks>
    /// This method routes the request to the appropriate dispatcher:
    /// - ICommand&lt;TResponse&gt; -> ICommandDispatcher
    /// - IQuery&lt;TResponse&gt; -> IQueryDispatcher
    /// - IRequest&lt;TResponse&gt; (direct implementation) -> Handled by Mediator with fallback logic
    /// 
    /// The routing is done at runtime based on the request type, allowing the mediator
    /// to act as a unified interface while delegating to specialized dispatchers.
    /// 
    /// For requests that implement IRequest&lt;TResponse&gt; directly (not recommended),
    /// the mediator falls back to directly resolving and executing the handler.
    /// </remarks>
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
    {
        // Route to appropriate dispatcher based on request type
        return request switch
        {
            ICommand<TResponse> command => _commandDispatcher.Dispatch(command, ct),
            IQuery<TResponse> query => _queryDispatcher.Dispatch(query, ct),
            // Fallback for requests that implement IRequest<TResponse> directly
            _ => HandleGenericRequest(request, ct)
        };
    }

    /// <summary>
    /// Handles requests that implement IRequest&lt;TResponse&gt; directly without implementing ICommand or IQuery.
    /// This provides backward compatibility for direct IRequest implementations.
    /// </summary>
    private async Task<TResponse> HandleGenericRequest<TResponse>(IRequest<TResponse> request, CancellationToken ct)
    {
        var key = (request.GetType(), typeof(TResponse));

        var dispatcher = _genericRequestCache.GetOrAdd(key, k =>
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
    /// <param name="request">The fire-and-forget request to send (must implement IRequest)</param>
    /// <param name="ct">Cancellation token for async operations</param>
    /// <returns>A task that completes when the handler finishes executing</returns>
    /// <remarks>
    /// This method handles fire-and-forget requests (IRequest without type parameter)
    /// which implement IRequest but not ICommand or IQuery.
    /// 
    /// Fire-and-forget requests are handled directly by the mediator because they use
    /// the IRequestHandler&lt;TRequest&gt; pattern (without TResponse) which doesn't
    /// fit cleanly into the ICommand/IQuery dispatcher model.
    /// 
    /// Process:
    /// 1. Resolves the handler implementing IRequestHandler&lt;TRequest&gt; (single type parameter)
    /// 2. Resolves all behaviors as IPipelineBehavior&lt;TRequest, Unit&gt; (Unit as response type)
    /// 3. Composes behaviors in reverse order with a Task-returning terminal
    /// 4. Invokes the composed pipeline with the request
    /// 5. Returns the task without unwrapping any response value
    /// 
    /// This is useful for operations that don't need to return a value,
    /// such as sending emails, publishing events, or executing background jobs.
    /// If no handler is registered, GetRequiredService() throws InvalidOperationException.
    /// </remarks>
    public Task Send(IRequest request, CancellationToken ct = default)
    {
        // Resolve strongly-typed handler - throws if not registered
        var key = (request.GetType(), typeof(Unit));

        var dispatcher = _fireAndForgetCache.GetOrAdd(key, k =>
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
