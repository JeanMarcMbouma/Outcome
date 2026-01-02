using System.Runtime.CompilerServices;

namespace BbQ.Events.Events;

/// <summary>
/// Interface for an event store that supports appending events and reading them sequentially.
/// </summary>
/// <remarks>
/// This interface is designed for testing projection engines with historical event replay.
/// For production use, implement this interface with a persistent event store.
/// </remarks>
public interface IEventStore
{
    /// <summary>
    /// Appends an event to a stream.
    /// </summary>
    /// <typeparam name="TEvent">The type of event</typeparam>
    /// <param name="stream">The stream name</param>
    /// <param name="event">The event to append</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The position of the appended event</returns>
    Task<long> AppendAsync<TEvent>(string stream, TEvent @event, CancellationToken ct = default);

    /// <summary>
    /// Reads events from a stream starting at a given position.
    /// </summary>
    /// <typeparam name="TEvent">The type of events to read</typeparam>
    /// <param name="stream">The stream name</param>
    /// <param name="fromPosition">The position to start reading from (inclusive)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>An async enumerable of events with their positions</returns>
    IAsyncEnumerable<StoredEvent<TEvent>> ReadAsync<TEvent>(
        string stream, 
        long fromPosition = 0, 
        CancellationToken ct = default);

    /// <summary>
    /// Gets the current position (last event position) in a stream.
    /// </summary>
    /// <param name="stream">The stream name</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The current position, or null if the stream doesn't exist</returns>
    Task<long?> GetStreamPositionAsync(string stream, CancellationToken ct = default);
}

/// <summary>
/// Represents an event stored in an event store with its position.
/// </summary>
/// <typeparam name="TEvent">The type of event</typeparam>
public record StoredEvent<TEvent>(long Position, TEvent Event);
