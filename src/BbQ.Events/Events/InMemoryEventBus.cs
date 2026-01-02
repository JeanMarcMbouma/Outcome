// -------------------------------
// In-memory Event Bus Implementation
// -------------------------------
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BbQ.Events.Events;

/// <summary>
/// In-memory implementation of IEventBus using System.Threading.Channels.
/// 
/// This implementation provides a lightweight, thread-safe event bus suitable for
/// single-process applications. Events are distributed to handlers and subscribers
/// without persistence, making it ideal for real-time event processing within
/// a single application instance.
/// </summary>
/// <remarks>
/// Features:
/// - Thread-safe event publishing and subscription
/// - Automatic cleanup of cancelled subscriptions
/// - Support for multiple concurrent subscribers
/// - Handles backpressure per subscriber (drops oldest messages for slow subscribers)
/// - Concurrent handler execution (awaited before publish completes)
/// 
/// Limitations:
/// - Events are not persisted (lost on application restart)
/// - Not suitable for distributed systems (single process only)
/// - Subscribers only receive events published after subscription
/// 
/// For production distributed systems, consider implementing IEventBus
/// with a message broker like RabbitMQ, Azure Service Bus, or Kafka.
/// </remarks>
internal sealed class InMemoryEventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InMemoryEventBus> _logger;
    
    // Dictionary of event type -> list of channels for that event type
    private readonly ConcurrentDictionary<Type, List<object>> _subscriptions = new();
    
    // Lock for managing subscription list modifications
    private readonly object _subscriptionLock = new();

    /// <summary>
    /// Initializes a new instance of the InMemoryEventBus.
    /// </summary>
    /// <param name="serviceProvider">Service provider for resolving event handlers</param>
    /// <param name="logger">Logger for diagnostic messages</param>
    public InMemoryEventBus(IServiceProvider serviceProvider, ILogger<InMemoryEventBus> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Publishes an event to all registered handlers and active subscribers.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to publish</typeparam>
    /// <param name="event">The event instance to publish</param>
    /// <param name="ct">Cancellation token for async operations</param>
    /// <returns>A task that completes when the event has been published to all channels</returns>
    public async Task Publish<TEvent>(TEvent @event, CancellationToken ct = default)
    {
        if (@event == null)
            throw new ArgumentNullException(nameof(@event));

        var eventType = typeof(TEvent);
        
        _logger.LogDebug("Publishing event of type {EventType}", eventType.Name);

        // Execute all registered event handlers
        await ExecuteHandlers(@event, ct);

        // Publish to all active subscribers
        await PublishToSubscribers(@event, ct);
    }

    /// <summary>
    /// Executes all registered IEventHandler&lt;TEvent&gt; instances for the given event.
    /// </summary>
    private async Task ExecuteHandlers<TEvent>(TEvent @event, CancellationToken ct)
    {
        var handlers = _serviceProvider.GetServices<IEventHandler<TEvent>>().ToList();

        if (handlers.Count == 0)
        {
            _logger.LogDebug("No handlers registered for event type {EventType}", typeof(TEvent).Name);
            return;
        }

        _logger.LogDebug("Executing {HandlerCount} handler(s) for event type {EventType}", 
            handlers.Count, typeof(TEvent).Name);

        // Execute all handlers concurrently and await their completion
        var tasks = handlers.Select(async handler =>
        {
            try
            {
                await handler.Handle(@event, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error executing event handler {HandlerType} for event type {EventType}", 
                    handler.GetType().Name, typeof(TEvent).Name);
            }
        });

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Publishes the event to all active subscriber channels.
    /// </summary>
    private async Task PublishToSubscribers<TEvent>(TEvent @event, CancellationToken ct)
    {
        var eventType = typeof(TEvent);
        
        List<Channel<TEvent>> activeChannels;
        lock (_subscriptionLock)
        {
            if (!_subscriptions.TryGetValue(eventType, out var channels) || channels.Count == 0)
            {
                _logger.LogDebug("No subscribers for event type {EventType}", eventType.Name);
                return;
            }
            
            // Get a snapshot of active channels (cast from object)
            activeChannels = channels.Cast<Channel<TEvent>>().ToList();
        }

        _logger.LogDebug("Publishing to {SubscriberCount} subscriber(s) for event type {EventType}", 
            activeChannels.Count, eventType.Name);

        // Write to all channels concurrently
        var tasks = activeChannels.Select(async channel =>
        {
            try
            {
                await channel.Writer.WriteAsync(@event, ct);
            }
            catch (ChannelClosedException)
            {
                // Channel was closed, will be cleaned up on next subscription check
                _logger.LogDebug("Attempted to write to closed channel for event type {EventType}", eventType.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing to channel for event type {EventType}", eventType.Name);
            }
        });

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Subscribes to a stream of events of the specified type.
    /// </summary>
    /// <typeparam name="TEvent">The type of events to subscribe to</typeparam>
    /// <param name="ct">Cancellation token for terminating the subscription</param>
    /// <returns>An asynchronous stream of events</returns>
    public async IAsyncEnumerable<TEvent> Subscribe<TEvent>([EnumeratorCancellation] CancellationToken ct = default)
    {
        var eventType = typeof(TEvent);
        
        // Create a bounded channel for this subscription (with capacity for backpressure)
        var channel = Channel.CreateBounded<TEvent>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest // Don't block publishers; drop oldest messages for slow subscribers
        });

        _logger.LogDebug("Creating new subscription for event type {EventType}", eventType.Name);

        // Register the channel
        lock (_subscriptionLock)
        {
            var channels = _subscriptions.GetOrAdd(eventType, _ => new List<object>());
            channels.Add(channel);
        }

        try
        {
            // Read from the channel until cancellation
            await foreach (var @event in channel.Reader.ReadAllAsync(ct))
            {
                yield return @event;
            }
        }
        finally
        {
            // Cleanup: close the channel and remove from subscriptions
            channel.Writer.Complete();
            
            lock (_subscriptionLock)
            {
                if (_subscriptions.TryGetValue(eventType, out var channels))
                {
                    channels.Remove(channel);
                    
                    // Remove the event type entry if no more subscribers
                    if (channels.Count == 0)
                    {
                        _subscriptions.TryRemove(eventType, out _);
                    }
                }
            }
            
            _logger.LogDebug("Subscription terminated for event type {EventType}", eventType.Name);
        }
    }
}
