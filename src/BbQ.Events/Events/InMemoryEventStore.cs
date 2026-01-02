using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace BbQ.Events.Events;

/// <summary>
/// In-memory implementation of IEventStore for testing projection engines.
/// </summary>
/// <remarks>
/// This implementation:
/// - Stores events in memory with sequential positions
/// - Supports multiple streams with independent position tracking
/// - Thread-safe for concurrent appends and reads
/// - Events are NOT lost on restart (unlike InMemoryEventBus)
/// - Ideal for testing projection replay and checkpointing
/// 
/// Usage in tests:
/// <code>
/// var store = new InMemoryEventStore();
/// 
/// // Seed historical events
/// await store.AppendAsync("users", new UserCreated(id1, "Alice"));
/// await store.AppendAsync("users", new UserCreated(id2, "Bob"));
/// 
/// // Read from beginning
/// await foreach (var stored in store.ReadAsync&lt;UserCreated&gt;("users"))
/// {
///     Console.WriteLine($"Position {stored.Position}: {stored.Event.Name}");
/// }
/// 
/// // Read from checkpoint
/// await foreach (var stored in store.ReadAsync&lt;UserCreated&gt;("users", fromPosition: 5))
/// {
///     // Process events after position 5
/// }
/// </code>
/// </remarks>
public class InMemoryEventStore : IEventStore
{
    private readonly ConcurrentDictionary<string, StreamData> _streams = new();
    
    /// <summary>
    /// Appends an event to a stream and returns its position.
    /// </summary>
    public Task<long> AppendAsync<TEvent>(string stream, TEvent @event, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(stream))
            throw new ArgumentException("Stream name cannot be null or empty", nameof(stream));
        
        if (@event == null)
            throw new ArgumentNullException(nameof(@event));
        
        var streamData = _streams.GetOrAdd(stream, _ => new StreamData());
        var position = streamData.Append(@event);
        
        return Task.FromResult(position);
    }

    /// <summary>
    /// Reads events from a stream starting at the specified position.
    /// </summary>
    public async IAsyncEnumerable<StoredEvent<TEvent>> ReadAsync<TEvent>(
        string stream, 
        long fromPosition = 0,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(stream))
            throw new ArgumentException("Stream name cannot be null or empty", nameof(stream));
        
        if (!_streams.TryGetValue(stream, out var streamData))
        {
            yield break; // Stream doesn't exist, return empty
        }

        var events = streamData.GetEvents<TEvent>(fromPosition);
        
        foreach (var storedEvent in events)
        {
            ct.ThrowIfCancellationRequested();
            yield return storedEvent;
        }
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets the current position (last event position) in a stream.
    /// </summary>
    public Task<long?> GetStreamPositionAsync(string stream, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(stream))
            throw new ArgumentException("Stream name cannot be null or empty", nameof(stream));
        
        if (_streams.TryGetValue(stream, out var streamData))
        {
            var position = streamData.GetCurrentPosition();
            return Task.FromResult<long?>(position);
        }
        
        return Task.FromResult<long?>(null);
    }

    /// <summary>
    /// Clears all streams (useful for test cleanup).
    /// </summary>
    public void Clear()
    {
        _streams.Clear();
    }

    /// <summary>
    /// Gets the total number of events across all streams.
    /// </summary>
    public int GetTotalEventCount()
    {
        return _streams.Values.Sum(s => s.GetEventCount());
    }

    /// <summary>
    /// Gets the number of events in a specific stream.
    /// </summary>
    public int GetStreamEventCount(string stream)
    {
        if (_streams.TryGetValue(stream, out var streamData))
        {
            return streamData.GetEventCount();
        }
        return 0;
    }

    /// <summary>
    /// Thread-safe storage for a single stream's events.
    /// </summary>
    private class StreamData
    {
        private readonly List<StoredEventData> _events = new();
        private readonly object _lock = new();
        private long _currentPosition = -1;

        public long Append(object @event)
        {
            lock (_lock)
            {
                _currentPosition++;
                var position = _currentPosition;
                _events.Add(new StoredEventData(position, @event));
                return position;
            }
        }

        public List<StoredEvent<TEvent>> GetEvents<TEvent>(long fromPosition)
        {
            lock (_lock)
            {
                return _events
                    .Where(e => e.Position >= fromPosition && e.Event is TEvent)
                    .Select(e => new StoredEvent<TEvent>(e.Position, (TEvent)e.Event))
                    .ToList();
            }
        }

        public long GetCurrentPosition()
        {
            lock (_lock)
            {
                return _currentPosition;
            }
        }

        public int GetEventCount()
        {
            lock (_lock)
            {
                return _events.Count;
            }
        }

        private record StoredEventData(long Position, object Event);
    }
}
