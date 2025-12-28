// -------------------------------
// Event/Pub-Sub contracts
// -------------------------------
namespace BbQ.Cqrs;

/// <summary>
/// Handler interface for processing events one-by-one as they are published.
/// 
/// Event handlers are invoked immediately when an event is published through
/// IEventPublisher. Multiple handlers can be registered for the same event type,
/// and all will be invoked when the event is published.
/// </summary>
/// <typeparam name="TEvent">The type of event to handle</typeparam>
/// <remarks>
/// Event handlers are optional. Events can be published without any handlers registered.
/// Handlers should be idempotent and handle errors gracefully, as failures in one
/// handler do not prevent other handlers from executing.
/// 
/// Example usage:
/// <code>
/// public class SendWelcomeEmailHandler : IEventHandler&lt;UserCreated&gt;
/// {
///     private readonly IEmailService _emailService;
///     
///     public SendWelcomeEmailHandler(IEmailService emailService)
///     {
///         _emailService = emailService;
///     }
///     
///     public async Task Handle(UserCreated evt, CancellationToken ct)
///     {
///         await _emailService.SendWelcomeEmail(evt.Email, evt.Name, ct);
///     }
/// }
/// </code>
/// 
/// Registration is automatic when using source generators:
/// <code>
/// // Handlers implementing IEventHandler&lt;TEvent&gt; are auto-discovered
/// services.AddYourAssemblyNameHandlers();
/// </code>
/// </remarks>
public interface IEventHandler<TEvent>
{
    /// <summary>
    /// Handles the event when it is published.
    /// </summary>
    /// <param name="event">The event to handle</param>
    /// <param name="ct">Cancellation token for async operations</param>
    /// <returns>A task that completes when the event has been handled</returns>
    /// <remarks>
    /// This method is called asynchronously when the event is published.
    /// Multiple handlers for the same event type are executed concurrently.
    /// 
    /// Guidelines:
    /// - Handlers should be idempotent (safe to execute multiple times)
    /// - Exceptions should be caught and logged; they won't prevent other handlers
    /// - Keep handlers lightweight; use background jobs for heavy processing
    /// - Use cancellation token for long-running operations
    /// </remarks>
    Task Handle(TEvent @event, CancellationToken ct = default);
}
