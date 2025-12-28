// -------------------------------
// Event/Pub-Sub contracts
// -------------------------------
namespace BbQ.Cqrs;

/// <summary>
/// Core event bus interface combining publishing and subscribing capabilities.
/// 
/// The event bus is the central hub for event-driven communication in the application.
/// It allows components to publish events without knowing about their consumers,
/// and enables consumers to subscribe to event streams without knowing about publishers.
/// </summary>
/// <remarks>
/// The event bus:
/// - Implements IEventPublisher for publishing events
/// - Provides subscription capabilities for consuming event streams
/// - Is storage-agnostic (can be in-memory, distributed, or persistent)
/// - Supports multiple handlers and subscribers per event type
/// - Does not require handlers or subscribers to be present
/// 
/// Typical usage pattern:
/// <code>
/// // Publishing events (via IEventPublisher)
/// await _eventBus.Publish(new UserCreated(userId, userName));
/// 
/// // Subscribing to events
/// await foreach (var evt in _eventBus.Subscribe&lt;UserCreated&gt;(ct))
/// {
///     Console.WriteLine($"User {evt.Name} was created");
/// }
/// </code>
/// 
/// Registration:
/// <code>
/// // Register the event bus
/// services.AddInMemoryEventBus();  // or your custom implementation
/// 
/// // The event bus is registered as both IEventBus and IEventPublisher
/// services.AddSingleton&lt;IEventPublisher&gt;(sp =&gt; sp.GetRequiredService&lt;IEventBus&gt;());
/// </code>
/// </remarks>
public interface IEventBus : IEventPublisher
{
    /// <summary>
    /// Subscribes to a stream of events of the specified type.
    /// </summary>
    /// <typeparam name="TEvent">The type of events to subscribe to</typeparam>
    /// <param name="ct">Cancellation token for terminating the subscription</param>
    /// <returns>An asynchronous stream of events</returns>
    /// <remarks>
    /// Creates a new subscription that will receive all events published after
    /// the subscription is created. Multiple subscribers can exist for the same
    /// event type, and each receives an independent copy of events.
    /// 
    /// Guidelines:
    /// - Each call creates a new independent subscription
    /// - Events are delivered in the order they were published
    /// - Slow subscribers won't block publishers or other subscribers
    /// - Use cancellation token to cleanly terminate the subscription
    /// - The stream completes when the cancellation token is triggered
    /// </remarks>
    IAsyncEnumerable<TEvent> Subscribe<TEvent>(CancellationToken ct = default);
}
