// -------------------------------
// Core contracts (Outcome-centric)
// -------------------------------
namespace BbQ.Cqrs;

/// <summary>
/// Marker interface for commands in the CQRS pattern.
/// 
/// Commands represent operations that modify state (create, update, delete).
/// They should be handled by exactly one handler and typically return
/// an Outcome&lt;T&gt; to enable comprehensive error handling.
/// </summary>
/// <typeparam name="TResponse">
/// The response type, typically an Outcome&lt;T&gt; for error-aware operations
/// </typeparam>
/// <remarks>
/// Usage pattern:
/// <code>
/// // Define the command
/// public class CreateUserCommand : ICommand&lt;Outcome&lt;User&gt;&gt;
/// {
///     public string Email { get; set; }
///     public string Name { get; set; }
/// }
/// 
/// // Implement the handler
/// public class CreateUserCommandHandler : IRequestHandler&lt;CreateUserCommand, Outcome&lt;User&gt;&gt;
/// {
///     public async Task&lt;Outcome&lt;User&gt;&gt; Handle(CreateUserCommand request, CancellationToken ct)
///     {
///         // Perform state-modifying operations
///         // Return success or failure wrapped in Outcome&lt;User&gt;
///     }
/// }
/// 
/// // Send the command
/// var result = await mediator.Send(new CreateUserCommand { ... });
/// </code>
/// 
/// Implementing classes should:
/// - Be immutable or effectively immutable
/// - Contain all data needed by the handler
/// - Follow a naming convention ending with "Command"
/// - Return Outcome&lt;T&gt; for proper error handling
/// </remarks>
public interface ICommand<TResponse> : IRequest<TResponse> { }
