// The command dispatcher implementation that coordinates the CQRS pipeline for commands
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace BbQ.Cqrs;

/// <summary>
/// The concrete implementation of ICommandDispatcher for the CQRS pattern.
/// 
/// This dispatcher:
/// 1. Resolves the handler for a given command
/// 2. Builds a pipeline of behaviors in registration order
/// 3. Executes the pipeline with the handler as the terminal
/// 4. Returns the response from the handler
/// 
/// This implementation uses reflection during the first dispatch of each
/// command/response type pair to build and cache the pipeline. Subsequent
/// dispatches reuse the cached pipeline without additional reflection
/// overhead, while handlers and behaviors are still resolved via
/// dependency injection.
/// </summary>
/// <remarks>
/// The dispatcher uses dependency injection to resolve:
/// - The specific IRequestHandler&lt;TCommand, TResponse&gt; implementation
/// - All registered IPipelineBehavior&lt;TCommand, TResponse&gt; implementations
/// 
/// Pipeline construction:
/// - Behaviors are retrieved from the service provider in registration order
/// - They are then composed in reverse order to form the chain
/// - This ensures the first registered behavior is the outermost
/// - The handler becomes the innermost (terminal) of the pipeline
/// 
/// Example pipeline for 2 behaviors:
/// <code>
/// command
///   -> Behavior1.Handle()
///        -> Behavior2.Handle()
///             -> Handler.Handle()
/// </code>
/// </remarks>
internal sealed class CommandDispatcher(IServiceProvider sp) : ICommandDispatcher
{
    private readonly IServiceProvider _sp = sp;

    private readonly ConcurrentDictionary<(Type Cmd, Type Res),
        Func<object, CancellationToken, Task>> _dispatchCache = new();

    /// <summary>
    /// Dispatches a command through the CQRS pipeline and returns a response.
    /// </summary>
    /// <typeparam name="TResponse">The response type</typeparam>
    /// <param name="command">The command to dispatch</param>
    /// <param name="ct">Cancellation token for async operations</param>
    /// <returns>The response from the handler after passing through all behaviors</returns>
    /// <remarks>
    /// Process:
    /// 1. Resolves the handler with GetRequiredService()
    /// 2. Resolves all behaviors with GetServices()
    /// 3. Composes behaviors in reverse order
    /// 4. Invokes the composed pipeline with the command
    /// 5. Returns the final response
    /// 
    /// If no handler is registered, GetRequiredService() throws InvalidOperationException.
    /// If no behaviors are registered, the command goes directly to the handler.
    /// </remarks>
    public async Task<TResponse> Dispatch<TResponse>(ICommand<TResponse> command, CancellationToken ct = default)
    {
        // Resolve strongly-typed handler - throws if not registered
        var key = (command.GetType(), typeof(TResponse));

        var dispatcher = _dispatchCache.GetOrAdd(key, k =>
        {
            var (cmdType, resType) = k;

            // Resolve handler
            var handlerType = typeof(IRequestHandler<,>).MakeGenericType(cmdType, resType);
            var handleMethod = handlerType.GetMethod("Handle")!;

            Task<TResponse> terminal(object cmd, CancellationToken token)
            {
                var handler = _sp.GetRequiredService(handlerType);
                return (Task<TResponse>)handleMethod.Invoke(handler, [cmd, token])!;
            }

            // Resolve behaviors and reverse order (last registered becomes outermost)
            var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(cmdType, resType);
            var behaviors = _sp.GetServices(behaviorType).Reverse().ToArray();

            Func<object, CancellationToken, Task<TResponse>> pipeline = terminal;
            foreach (var b in behaviors)
            {
                var method = behaviorType.GetMethod("Handle")!;
                var next = pipeline;
                pipeline = (cmd, token) =>
                    (Task<TResponse>)method.Invoke(b,
                    [
                            cmd,
                            token,
                            new Func<object, CancellationToken, Task<TResponse>>(next)
                    ])!;
            }

            return pipeline;
        });

        return await (Task<TResponse>)dispatcher(command, ct);
    }

    /// <summary>
    /// Dispatches a fire-and-forget command through the CQRS pipeline.
    /// </summary>
    /// <param name="command">The command to dispatch</param>
    /// <param name="ct">Cancellation token for async operations</param>
    /// <returns>A task that completes when the handler finishes executing</returns>
    /// <remarks>
    /// Process:
    /// 1. Resolves the handler implementing IRequestHandler&lt;TCommand, Unit&gt;
    /// 2. Resolves all behaviors as IPipelineBehavior&lt;TCommand, Unit&gt;
    /// 3. Composes behaviors in reverse order
    /// 4. Invokes the composed pipeline with the command
    /// 5. Returns the task without unwrapping any response value
    /// 
    /// This overload is useful for commands that don't need to return a value,
    /// such as sending emails, publishing events, or executing background jobs.
    /// If no handler is registered, GetRequiredService() throws InvalidOperationException.
    /// </remarks>
    public Task Dispatch(ICommand<Unit> command, CancellationToken ct = default)
    {
        return Dispatch<Unit>(command, ct);
    }
}
