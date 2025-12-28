// -------------------------------
// Event/Pub-Sub contracts
// -------------------------------
namespace BbQ.Cqrs;

/// <summary>
/// Publisher interface for publishing events in the pub/sub pattern.
/// 
/// Events are published asynchronously and can be handled by multiple event handlers
/// or consumed through event subscribers. Publishing an event does not require
/// any handlers or subscribers to be registered.
/// </summary>
/// <remarks>
/// This interface is typically implemented by an event bus and allows command handlers
/// to publish domain events after state changes. Events are fire-and-forget by default.
/// 
/// Example usage in a command handler:
/// <code>
/// public class CreateUserHandler : IRequestHandler&lt;CreateUser, Outcome&lt;User&gt;&gt;
/// {
///     private readonly IEventPublisher _publisher;
///     
///     public CreateUserHandler(IEventPublisher publisher)
///     {
///         _publisher = publisher;
///     }
///     
///     public async Task&lt;Outcome&lt;User&gt;&gt; Handle(CreateUser command, CancellationToken ct)
///     {
///         // ... create user logic ...
///         
///         // Publish event after state change
///         await _publisher.Publish(new UserCreated(user.Id, user.Name), ct);
///         
///         return Outcome&lt;User&gt;.From(user);
///     }
/// }
/// </code>
/// </remarks>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes an event to all registered handlers and subscribers.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to publish</typeparam>
    /// <param name="event">The event instance to publish</param>
    /// <param name="ct">Cancellation token for async operations</param>
    /// <returns>A task that completes when the event has been published</returns>
    /// <remarks>
    /// This method publishes the event to:
    /// - All registered IEventHandler&lt;TEvent&gt; instances (executed immediately)
    /// - All IEventSubscriber&lt;TEvent&gt; streams (queued for consumption)
    /// 
    /// Publishing is non-blocking and does not wait for handlers to complete.
    /// If no handlers or subscribers are registered, the event is silently ignored.
    /// </remarks>
    Task Publish<TEvent>(TEvent @event, CancellationToken ct = default);
}
