// -------------------------------
// Core contracts (Outcome-centric)
// -------------------------------
namespace BbQ.Cqrs;

/// <summary>
/// Dispatcher interface for commands in the CQRS pattern.
/// 
/// The command dispatcher is responsible for:
/// - Taking a command request
/// - Resolving the correct handler
/// - Applying pipeline behaviors in order
/// - Executing the handler
/// - Returning the result
/// 
/// This interface defines a thin orchestrator over the command pipeline.
/// </summary>
/// <remarks>
/// Commands represent operations that modify state (create, update, delete).
/// The dispatcher ensures that all registered behaviors are applied before
/// the command reaches its handler.
/// 
/// Example usage:
/// <code>
/// public class UserController
/// {
///     private readonly ICommandDispatcher _commandDispatcher;
///     
///     public async Task&lt;IActionResult&gt; CreateUser(CreateUserCommand command, CancellationToken ct)
///     {
///         var result = await _commandDispatcher.Dispatch(command, ct);
///         return result.Match(
///             onSuccess: user => Ok(user),
///             onError: errors => BadRequest(errors)
///         );
///     }
/// }
/// </code>
/// </remarks>
public interface ICommandDispatcher
{
    /// <summary>
    /// Dispatches a command through the pipeline and returns a response.
    /// </summary>
    /// <typeparam name="TResponse">The response type returned by the command handler</typeparam>
    /// <param name="command">The command to dispatch</param>
    /// <param name="ct">Optional cancellation token for async operations</param>
    /// <returns>A task containing the response from the command handler</returns>
    /// <remarks>
    /// The command is passed through all registered IPipelineBehavior implementations
    /// in registration order. First registered behavior becomes outermost, creating:
    /// - FIFO (First In, First Out) order before handler execution
    /// - LIFO (Last In, First Out) order after handler execution
    /// 
    /// This means behaviors wrap like nested function calls:
    /// Behavior1 → Behavior2 → Handler → Behavior2 → Behavior1
    /// 
    /// Process:
    /// 1. Resolves the handler for the command type
    /// 2. Builds the pipeline with all registered behaviors
    /// 3. Executes behaviors in registration order (first registered executes first)
    /// 4. Invokes the handler
    /// 5. Returns through behaviors in reverse order (first registered returns last)
    /// </remarks>
    Task<TResponse> Dispatch<TResponse>(ICommand<TResponse> command, CancellationToken ct = default);

    /// <summary>
    /// Dispatches a fire-and-forget command through the pipeline.
    /// </summary>
    /// <param name="command">The command to dispatch</param>
    /// <param name="ct">Optional cancellation token for async operations</param>
    /// <returns>A task that completes when the command has been handled</returns>
    /// <remarks>
    /// Used for commands that don't return a meaningful value, such as
    /// sending notifications or executing background operations.
    /// </remarks>
    Task Dispatch(ICommand<Unit> command, CancellationToken ct = default);
}
