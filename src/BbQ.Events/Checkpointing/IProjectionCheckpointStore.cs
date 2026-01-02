namespace BbQ.Events.Checkpointing;

/// <summary>
/// Storage interface for managing projection checkpoints.
/// 
/// Checkpoints track the position in the event stream up to which a projection
/// has been processed. This enables projections to resume from their last
/// processed position after restarts or failures.
/// </summary>
/// <remarks>
/// Checkpoint storage:
/// - Must be durable (survive application restarts)
/// - Should support atomic updates (prevent duplicate processing)
/// - Can be implemented using databases, distributed caches, or file systems
/// 
/// The checkpoint is typically an offset, sequence number, or timestamp that
/// identifies a position in the event stream.
/// </remarks>
public interface IProjectionCheckpointStore
{
    /// <summary>
    /// Gets the last checkpoint for a specific projection.
    /// </summary>
    /// <param name="projectionName">The unique name of the projection</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The checkpoint position, or null if no checkpoint exists</returns>
    /// <remarks>
    /// Returns null when:
    /// - The projection is being run for the first time
    /// - The checkpoint has been reset
    /// 
    /// The projection engine uses this to determine where to start processing events.
    /// </remarks>
    ValueTask<long?> GetCheckpointAsync(string projectionName, CancellationToken ct = default);

    /// <summary>
    /// Saves a checkpoint for a specific projection.
    /// </summary>
    /// <param name="projectionName">The unique name of the projection</param>
    /// <param name="checkpoint">The checkpoint position to save</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A task that completes when the checkpoint has been saved</returns>
    /// <remarks>
    /// This method should:
    /// - Atomically update the checkpoint (prevent race conditions)
    /// - Be idempotent (safe to call multiple times with the same value)
    /// - Persist durably (survive application restarts)
    /// 
    /// The projection engine calls this after successfully processing a batch of events.
    /// </remarks>
    ValueTask SaveCheckpointAsync(string projectionName, long checkpoint, CancellationToken ct = default);

    /// <summary>
    /// Resets the checkpoint for a specific projection.
    /// </summary>
    /// <param name="projectionName">The unique name of the projection</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A task that completes when the checkpoint has been reset</returns>
    /// <remarks>
    /// Resetting a checkpoint causes the projection to restart from the beginning
    /// of the event stream. This is useful for:
    /// - Rebuilding projections after schema changes
    /// - Recovering from corrupted projection state
    /// - Testing projection logic
    /// </remarks>
    ValueTask ResetCheckpointAsync(string projectionName, CancellationToken ct = default);
}
