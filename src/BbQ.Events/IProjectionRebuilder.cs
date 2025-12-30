namespace BbQ.Events;

/// <summary>
/// Service for rebuilding projections from scratch by resetting checkpoints.
/// 
/// The rebuilder provides APIs to reset projection checkpoints, causing projections
/// to replay all events from the beginning. This is useful for:
/// - Rebuilding projections after schema changes
/// - Recovering from corrupted projection state
/// - Testing projection logic
/// - Migrating to new projection implementations
/// </summary>
/// <remarks>
/// After resetting checkpoints, the projection engine must be restarted (or projections
/// restarted) for the changes to take effect. The rebuilder only manages checkpoints -
/// it does not modify projection state or read models directly.
/// 
/// For partitioned projections, you can reset:
/// - All partitions of a projection (using ResetProjectionAsync)
/// - A specific partition (using ResetPartitionAsync)
/// 
/// Usage:
/// <code>
/// // Reset all projections
/// await rebuilder.ResetAllProjectionsAsync(ct);
/// 
/// // Reset a specific projection
/// await rebuilder.ResetProjectionAsync("UserProfileProjection", ct);
/// 
/// // Reset a specific partition
/// await rebuilder.ResetPartitionAsync("UserStatisticsProjection", "user-123", ct);
/// </code>
/// </remarks>
public interface IProjectionRebuilder
{
    /// <summary>
    /// Resets all registered projections, causing them to rebuild from the beginning.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A task that completes when all projections have been reset</returns>
    /// <remarks>
    /// This method:
    /// - Resets the main projection checkpoints for all registered projections
    /// - For partitioned projections, resets only the main projection checkpoint and does not reset individual partition checkpoints
    /// - Does not modify projection state or read models
    /// 
    /// After calling this method, restart the projection engine or projections
    /// to begin the rebuild process.
    /// 
    /// Example:
    /// <code>
    /// // Reset all projections
    /// await rebuilder.ResetAllProjectionsAsync(ct);
    /// 
    /// // Restart projection engine to begin rebuild
    /// await engine.RunAsync(ct);
    /// </code>
    /// </remarks>
    ValueTask ResetAllProjectionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Resets a specific projection, causing it to rebuild from the beginning.
    /// </summary>
    /// <param name="projectionName">The name of the projection to reset</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A task that completes when the projection has been reset</returns>
    /// <remarks>
    /// This method:
    /// - Resets the checkpoint for the specified projection
    /// - For non-partitioned projections, clears the main checkpoint
    /// - For partitioned projections, clears the main projection checkpoint only
    /// - Does not automatically reset individual partition checkpoints
    /// - Does not modify projection state or read models
    /// 
    /// The projection name should match the name used when registering the projection,
    /// which is typically the class name (e.g., "UserProfileProjection").
    /// 
    /// For partitioned projections: This method resets the main projection checkpoint,
    /// but existing partition checkpoints (created dynamically as events are processed)
    /// remain unchanged. To reset individual partitions, use ResetPartitionAsync.
    /// To reset all checkpoints including partitions, first call ResetProjectionAsync,
    /// then call ResetPartitionAsync for each known partition.
    /// 
    /// After calling this method, restart the projection engine or projection
    /// to begin the rebuild process.
    /// 
    /// Example:
    /// <code>
    /// // Reset a specific projection
    /// await rebuilder.ResetProjectionAsync("UserProfileProjection", ct);
    /// 
    /// // For partitioned projections, also reset known partitions if needed
    /// await rebuilder.ResetPartitionAsync("UserStatisticsProjection", "user-123", ct);
    /// await rebuilder.ResetPartitionAsync("UserStatisticsProjection", "user-456", ct);
    /// 
    /// // Restart projection engine to begin rebuild
    /// await engine.RunAsync(ct);
    /// </code>
    /// </remarks>
    ValueTask ResetProjectionAsync(string projectionName, CancellationToken ct = default);

    /// <summary>
    /// Resets a specific partition of a partitioned projection, causing it to rebuild from the beginning.
    /// </summary>
    /// <param name="projectionName">The name of the projection</param>
    /// <param name="partitionKey">The partition key to reset</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A task that completes when the partition has been reset</returns>
    /// <remarks>
    /// This method:
    /// - Resets the checkpoint for the specified partition only
    /// - Other partitions of the same projection are not affected
    /// - Does not modify projection state or read models
    /// 
    /// This is useful for:
    /// - Rebuilding a single partition without affecting others
    /// - Recovering from errors in specific partitions
    /// - Testing partition-specific logic
    /// 
    /// After calling this method, restart the projection engine or projection
    /// to begin the rebuild process for this partition.
    /// 
    /// Example:
    /// <code>
    /// // Reset a specific partition
    /// await rebuilder.ResetPartitionAsync("UserStatisticsProjection", "user-123", ct);
    /// 
    /// // Restart projection engine to begin rebuild
    /// await engine.RunAsync(ct);
    /// </code>
    /// </remarks>
    ValueTask ResetPartitionAsync(string projectionName, string partitionKey, CancellationToken ct = default);

    /// <summary>
    /// Gets all registered projection names.
    /// </summary>
    /// <returns>A collection of registered projection names</returns>
    /// <remarks>
    /// This method is useful for:
    /// - Discovering available projections for CLI tools
    /// - Validating projection names before resetting
    /// - Building management UIs
    /// 
    /// Example:
    /// <code>
    /// var projections = rebuilder.GetRegisteredProjections();
    /// foreach (var projection in projections)
    /// {
    ///     Console.WriteLine($"Projection: {projection}");
    /// }
    /// </code>
    /// </remarks>
    IEnumerable<string> GetRegisteredProjections();
}
