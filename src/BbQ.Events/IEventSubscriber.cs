// -------------------------------
// Event/Pub-Sub contracts
// -------------------------------
namespace BbQ.Events;

/// <summary>
/// Subscriber interface for consuming events as a stream.
/// 
/// Event subscribers provide a way to consume events as an IAsyncEnumerable stream,
/// allowing for reactive programming patterns and backpressure management.
/// This is useful for scenarios like event sourcing, real-time dashboards,
/// or forwarding events to external systems.
/// </summary>
/// <typeparam name="TEvent">The type of events to subscribe to</typeparam>
/// <remarks>
/// Event subscribers are optional. Events can be published without any subscribers.
/// Multiple subscribers can be registered for the same event type, and each will
/// receive a copy of all published events.
/// 
/// Example usage:
/// <code>
/// public class UserCreatedStreamSubscriber : IEventSubscriber&lt;UserCreated&gt;
/// {
///     private readonly IEventStore _store;
///     
///     public UserCreatedStreamSubscriber(IEventStore store)
///     {
///         _store = store;
///     }
///     
///     public IAsyncEnumerable&lt;UserCreated&gt; Subscribe(CancellationToken ct)
///     {
///         // Forward events from event bus to persistent store
///         return _store.Subscribe&lt;UserCreated&gt;("users", ct);
///     }
/// }
/// </code>
/// 
/// Consuming events from a subscriber:
/// <code>
/// public class UserEventProcessor : BackgroundService
/// {
///     private readonly IEventSubscriber&lt;UserCreated&gt; _subscriber;
///     
///     protected override async Task ExecuteAsync(CancellationToken ct)
///     {
///         await foreach (var evt in _subscriber.Subscribe(ct))
///         {
///             // Process event from stream
///             Console.WriteLine($"User created: {evt.Name}");
///         }
///     }
/// }
/// </code>
/// 
/// Registration is automatic when using source generators:
/// <code>
/// // Subscribers implementing IEventSubscriber&lt;TEvent&gt; are auto-discovered
/// services.AddYourAssemblyNameHandlers();
/// </code>
/// </remarks>
public interface IEventSubscriber<TEvent>
{
    /// <summary>
    /// Subscribes to a stream of events of the specified type.
    /// </summary>
    /// <param name="ct">Cancellation token for terminating the subscription</param>
    /// <returns>An asynchronous stream of events</returns>
    /// <remarks>
    /// The returned stream will emit all events published after subscription begins.
    /// The stream remains active until cancelled via the cancellation token.
    /// 
    /// Guidelines:
    /// - Each subscriber receives its own independent stream
    /// - Events are delivered in the order they were published
    /// - Slow subscribers won't block publishers or other subscribers
    /// - Use cancellation token to cleanly terminate the subscription
    /// </remarks>
    IAsyncEnumerable<TEvent> Subscribe(CancellationToken ct = default);
}
