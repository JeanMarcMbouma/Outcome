// The core mediator implementation that coordinates the CQRS pipeline
using BbQ.Cqrs;
using Microsoft.Extensions.DependencyInjection;

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
    public Task<TResponse> Send<TRequest, TResponse>(TRequest request, CancellationToken ct = default)
        where TRequest : IRequest<TResponse>
    {
        // Resolve strongly-typed handler - throws if not registered
        var handler = _sp.GetRequiredService<IRequestHandler<TRequest, TResponse>>();

        // Resolve all behaviors for this request/response pair in registration order
        // Microsoft DI preserves registration order
        var behaviors = _sp.GetServices<IPipelineBehavior<TRequest, TResponse>>();

        // Start with the handler as the terminal of the pipeline
        Func<TRequest, CancellationToken, Task<TResponse>> terminal =
            (req, token) => handler.Handle(req, token);

        // Compose behaviors in reverse order so first registered is outermost
        foreach (var behavior in behaviors.Reverse())
        {
            var next = terminal;
            terminal = (req, token) => behavior.Handle(req, token, next);
        }

        // Execute the fully composed pipeline
        return terminal(request, ct);
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
    /// 1. Resolves the handler with GetRequiredService()
    /// 2. Resolves all behaviors with GetServices()
    /// 3. Composes behaviors in reverse order with a Unit-returning terminal
    /// 4. Invokes the composed pipeline with the request
    /// 5. Discards the Unit result before returning
    /// 
    /// This overload is useful for operations that don't need to return a value,
    /// such as sending emails, publishing events, or executing background jobs.
    /// If no handler is registered, GetRequiredService() throws InvalidOperationException.
    /// </remarks>
    public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
    {
        // Resolve strongly-typed handler - throws if not registered
        var handler = _sp.GetRequiredService<IRequestHandler<TRequest>>();

        // Resolve all behaviors for this request/response pair in registration order
        // Microsoft DI preserves registration order
        var behaviors = _sp.GetServices<IPipelineBehavior<TRequest, Unit>>();

        // Start with the handler as the terminal of the pipeline
        Func<TRequest, CancellationToken, Task<Unit>> terminal =
            (req, token) =>
            {
                handler.Handle(req, token);
                return Task.FromResult(Unit.Value);
            };

        // Compose behaviors in reverse order so first registered is outermost
        foreach (var behavior in behaviors.Reverse())
        {
            var next = terminal;
            terminal = (req, token) => behavior.Handle(req, token, next);
        }

        // Execute the fully composed pipeline
        return terminal(request, ct);
    }
}
