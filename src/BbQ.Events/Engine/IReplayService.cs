namespace BbQ.Events.Engine;

/// <summary>
/// Service for orchestrating projection replay operations.
/// </summary>
/// <remarks>
/// The replay service provides first-class support for rebuilding projections from
/// historical events. Unlike simple checkpoint resets, replay offers:
/// - Fine-grained control over replay boundaries (FromPosition, ToPosition)
/// - Batch processing configuration
/// - Dry run mode for testing
/// - Flexible checkpoint strategies
/// - Support for partitioned projections
/// 
/// Replay is explicit and opt-in. It does not run automatically or in the background.
/// 
/// Usage examples:
/// 
/// Basic replay from scratch:
/// <code>
/// await replayService.ReplayAsync(
///     "UserProfileProjection",
///     new ReplayOptions { FromPosition = 0 },
///     cancellationToken);
/// </code>
/// 
/// Replay a specific range:
/// <code>
/// await replayService.ReplayAsync(
///     "OrderProjection",
///     new ReplayOptions 
///     { 
///         FromPosition = 1000,
///         ToPosition = 2000,
///         BatchSize = 100
///     },
///     cancellationToken);
/// </code>
/// 
/// Dry run replay (no checkpoint writes):
/// <code>
/// await replayService.ReplayAsync(
///     "InventoryProjection",
///     new ReplayOptions 
///     { 
///         DryRun = true,
///         FromPosition = 0
///     },
///     cancellationToken);
/// </code>
/// 
/// Replay a specific partition:
/// <code>
/// await replayService.ReplayAsync(
///     "UserStatisticsProjection",
///     new ReplayOptions 
///     { 
///         Partition = "user-123",
///         FromPosition = 0
///     },
///     cancellationToken);
/// </code>
/// </remarks>
public interface IReplayService
{
    /// <summary>
    /// Replays a projection from historical events.
    /// </summary>
    /// <param name="projectionName">The name of the projection to replay</param>
    /// <param name="options">Configuration options for the replay operation</param>
    /// <param name="cancellationToken">Cancellation token to stop the replay</param>
    /// <returns>A task that completes when replay finishes or is cancelled</returns>
    /// <exception cref="ArgumentException">Thrown when projectionName is null or empty</exception>
    /// <exception cref="ArgumentNullException">Thrown when options is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when projection is not found or options are invalid</exception>
    /// <remarks>
    /// This method:
    /// - Validates replay options
    /// - Resolves projection metadata  
    /// - Determines replay boundaries
    /// - Optionally resets checkpoints
    /// - Streams events from IEventStore (if registered)
    /// - Processes events through projection handlers
    /// - Writes checkpoints based on CheckpointMode
    /// - Provides progress tracking via logging
    /// 
    /// The method blocks until replay completes or is cancelled.
    /// For long-running replays, monitor progress through logging.
    /// 
    /// Error Handling: By default, projection errors are logged and replay continues.
    /// This behavior ensures replay can complete even if some events fail processing.
    /// 
    /// Stream Naming: Events are read from a stream named after the projection.
    /// Ensure events for the projection are appended to a stream with this name.
    /// </remarks>
    Task ReplayAsync(
        string projectionName,
        ReplayOptions options,
        CancellationToken cancellationToken = default);
}
