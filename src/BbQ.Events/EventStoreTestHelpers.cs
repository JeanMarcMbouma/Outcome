namespace BbQ.Events;

/// <summary>
/// Helper extensions for working with IEventStore in tests.
/// </summary>
public static class EventStoreTestHelpers
{
    /// <summary>
    /// Seeds multiple events into a stream in a single call.
    /// </summary>
    /// <typeparam name="TEvent">The type of events</typeparam>
    /// <param name="store">The event store</param>
    /// <param name="stream">The stream name</param>
    /// <param name="events">The events to seed</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The positions of the appended events</returns>
    public static async Task<long[]> SeedEventsAsync<TEvent>(
        this IEventStore store,
        string stream,
        IEnumerable<TEvent> events,
        CancellationToken ct = default)
    {
        var positions = new List<long>();
        
        foreach (var @event in events)
        {
            var position = await store.AppendAsync(stream, @event, ct);
            positions.Add(position);
        }
        
        return positions.ToArray();
    }

    /// <summary>
    /// Seeds multiple events into a stream in a single call (params version).
    /// </summary>
    /// <typeparam name="TEvent">The type of events</typeparam>
    /// <param name="store">The event store</param>
    /// <param name="stream">The stream name</param>
    /// <param name="events">The events to seed</param>
    /// <returns>The positions of the appended events</returns>
    public static Task<long[]> SeedEventsAsync<TEvent>(
        this IEventStore store,
        string stream,
        params TEvent[] events)
    {
        return store.SeedEventsAsync(stream, events, CancellationToken.None);
    }

    /// <summary>
    /// Reads all events from a stream into a list.
    /// </summary>
    /// <typeparam name="TEvent">The type of events</typeparam>
    /// <param name="store">The event store</param>
    /// <param name="stream">The stream name</param>
    /// <param name="fromPosition">The position to start reading from</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A list of stored events</returns>
    public static async Task<List<StoredEvent<TEvent>>> ReadAllAsync<TEvent>(
        this IEventStore store,
        string stream,
        long fromPosition = 0,
        CancellationToken ct = default)
    {
        var events = new List<StoredEvent<TEvent>>();
        
        await foreach (var storedEvent in store.ReadAsync<TEvent>(stream, fromPosition, ct))
        {
            events.Add(storedEvent);
        }
        
        return events;
    }

    /// <summary>
    /// Reads only the event data (without positions) into a list.
    /// </summary>
    /// <typeparam name="TEvent">The type of events</typeparam>
    /// <param name="store">The event store</param>
    /// <param name="stream">The stream name</param>
    /// <param name="fromPosition">The position to start reading from</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A list of events</returns>
    public static async Task<List<TEvent>> ReadEventsAsync<TEvent>(
        this IEventStore store,
        string stream,
        long fromPosition = 0,
        CancellationToken ct = default)
    {
        var events = new List<TEvent>();
        
        await foreach (var storedEvent in store.ReadAsync<TEvent>(stream, fromPosition, ct))
        {
            events.Add(storedEvent.Event);
        }
        
        return events;
    }

    /// <summary>
    /// Counts the number of events in a stream.
    /// </summary>
    /// <typeparam name="TEvent">The type of events</typeparam>
    /// <param name="store">The event store</param>
    /// <param name="stream">The stream name</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The number of events</returns>
    public static async Task<int> CountEventsAsync<TEvent>(
        this IEventStore store,
        string stream,
        CancellationToken ct = default)
    {
        var count = 0;
        
        await foreach (var _ in store.ReadAsync<TEvent>(stream, 0, ct))
        {
            count++;
        }
        
        return count;
    }
}
