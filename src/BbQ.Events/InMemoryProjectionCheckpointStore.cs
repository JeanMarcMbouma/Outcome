using System.Collections.Concurrent;

namespace BbQ.Events;

/// <summary>
/// In-memory implementation of projection checkpoint storage.
/// 
/// This implementation stores checkpoints in memory and is suitable for:
/// - Development and testing
/// - Single-instance applications where checkpoints don't need to survive restarts
/// - Prototyping and experimentation
/// </summary>
/// <remarks>
/// WARNING: Checkpoints stored with this implementation are lost on application restart.
/// For production use, implement IProjectionCheckpointStore using a durable storage
/// backend such as:
/// - SQL databases (PostgreSQL, SQL Server, etc.)
/// - NoSQL databases (MongoDB, Redis, etc.)
/// - Distributed caches
/// - File systems
/// </remarks>
public class InMemoryProjectionCheckpointStore : IProjectionCheckpointStore
{
    private readonly ConcurrentDictionary<string, long> _checkpoints = new();

    /// <summary>
    /// Gets the last checkpoint for a specific projection.
    /// </summary>
    /// <param name="projectionName">The unique name of the projection</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The checkpoint position, or null if no checkpoint exists</returns>
    public ValueTask<long?> GetCheckpointAsync(string projectionName, CancellationToken ct = default)
    {
        if (_checkpoints.TryGetValue(projectionName, out var checkpoint))
        {
            return ValueTask.FromResult<long?>(checkpoint);
        }
        return ValueTask.FromResult<long?>(null);
    }

    /// <summary>
    /// Saves a checkpoint for a specific projection.
    /// </summary>
    /// <param name="projectionName">The unique name of the projection</param>
    /// <param name="checkpoint">The checkpoint position to save</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A task that completes when the checkpoint has been saved</returns>
    public ValueTask SaveCheckpointAsync(string projectionName, long checkpoint, CancellationToken ct = default)
    {
        _checkpoints.AddOrUpdate(projectionName, checkpoint, (_, _) => checkpoint);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Resets the checkpoint for a specific projection.
    /// </summary>
    /// <param name="projectionName">The unique name of the projection</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A task that completes when the checkpoint has been reset</returns>
    public ValueTask ResetCheckpointAsync(string projectionName, CancellationToken ct = default)
    {
        _checkpoints.TryRemove(projectionName, out _);
        return ValueTask.CompletedTask;
    }
}
