// -------------------------------
// Core contracts (Outcome-centric)
// -------------------------------
namespace BbQ.Cqrs;

/// <summary>
/// Base marker interface for all requests in the CQRS pipeline.
/// 
/// Represents a single operation (command or query) that will be sent through
/// the mediator to be handled by an IRequestHandler&lt;TRequest, TResponse&gt;.
/// </summary>
/// <typeparam name="TResponse">The type of response expected from handling this request</typeparam>
/// <remarks>
/// This interface serves as the constraint for the mediator's generic Send method.
/// Implement either ICommand&lt;TResponse&gt; or IQuery&lt;TResponse&gt; instead of 
/// directly implementing this interface.
/// 
/// Example:
/// <code>
/// public class GetUserByIdQuery : IQuery&lt;User&gt; { }
/// public class CreateUserCommand : ICommand&lt;Outcome&lt;User&gt;&gt; { }
/// </code>
/// </remarks>
public interface IRequest<TResponse> { }

/// <summary>
/// Marker interface for requests that don't return a meaningful value (void-like).
/// 
/// This is a convenience interface that simplifies defining fire-and-forget operations
/// that have no meaningful return value. It aliases IRequest&lt;Unit&gt;.
/// </summary>
/// <remarks>
/// Use IRequest for operations where you don't care about the return value,
/// such as sending emails, publishing events, or updating caches.
/// 
/// The handler for IRequest implements IRequestHandler&lt;TRequest&gt;
/// (without TResponse) and doesn't need to return anything meaningful.
/// 
/// Example:
/// <code>
/// // A fire-and-forget command
/// public class SendNotificationCommand : IRequest
/// {
///     public string UserId { get; set; }
///     public string Message { get; set; }
/// }
/// 
/// // Handler just performs the action, no return value
/// public class SendNotificationHandler : IRequestHandler&lt;SendNotificationCommand&gt;
/// {
///     public async Task Handle(SendNotificationCommand request, CancellationToken ct)
///     {
///         await _notificationService.SendAsync(request.UserId, request.Message, ct);
///     }
/// }
/// 
/// // Send the command (result is discarded)
/// await mediator.Send(new SendNotificationCommand { ... });
/// </code>
/// </remarks>
public interface IRequest : IRequest<Unit>;
