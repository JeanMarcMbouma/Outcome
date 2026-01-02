namespace BbQ.Events.Engine;

/// <summary>
/// Configuration options for replaying projections from historical events.
/// </summary>
/// <remarks>
/// ReplayOptions control how a projection replay operation is executed:
/// - FromCheckpoint: Resume from the last saved checkpoint position
/// - FromPosition: Start replay from a specific event position
/// - ToPosition: Stop replay at a specific event position
/// - BatchSize: Number of events to process before checkpointing
/// - Partition: Replay only a specific partition (for partitioned projections)
/// - DryRun: Process events without persisting checkpoints
/// - CheckpointMode: Control when checkpoints are written during replay
/// 
/// Example usage:
/// <code>
/// var options = new ReplayOptions
/// {
///     FromPosition = 0,
///     ToPosition = 1000,
///     BatchSize = 50,
///     DryRun = true
/// };
/// await replayService.ReplayAsync("UserProfileProjection", options, ct);
/// </code>
/// </remarks>
public class ReplayOptions
{
    /// <summary>
    /// When true, starts replay from the last saved checkpoint position.
    /// When false, starts from FromPosition or the beginning if FromPosition is null.
    /// </summary>
    /// <remarks>
    /// Default: false
    /// 
    /// If both FromCheckpoint and FromPosition are set, FromPosition takes precedence.
    /// </remarks>
    public bool FromCheckpoint { get; set; } = false;

    /// <summary>
    /// The event position to start replay from.
    /// If null, replay starts from the beginning (position 0).
    /// </summary>
    /// <remarks>
    /// Default: null (start from beginning)
    /// 
    /// This option takes precedence over FromCheckpoint.
    /// Use this to replay events from a specific point in the event stream.
    /// </remarks>
    public long? FromPosition { get; set; }

    /// <summary>
    /// The event position to stop replay at (inclusive).
    /// If null, replay processes all available events.
    /// </summary>
    /// <remarks>
    /// Default: null (process all events)
    /// 
    /// Use this to replay a specific range of events, useful for:
    /// - Testing projection logic with a subset of events
    /// - Debugging specific event sequences
    /// - Incremental replay in stages
    /// </remarks>
    public long? ToPosition { get; set; }

    /// <summary>
    /// Number of events to process before persisting a checkpoint.
    /// If null, uses the projection's default CheckpointBatchSize.
    /// </summary>
    /// <remarks>
    /// Default: null (use projection default)
    /// 
    /// Smaller values provide better recovery granularity but increase checkpoint overhead.
    /// Larger values reduce checkpoint overhead but may require reprocessing more events on restart.
    /// 
    /// Ignored when DryRun is true or CheckpointMode is None.
    /// </remarks>
    public int? BatchSize { get; set; }

    /// <summary>
    /// The partition key to replay.
    /// If null, replays all partitions (non-partitioned or all partitions).
    /// </summary>
    /// <remarks>
    /// Default: null (all partitions)
    /// 
    /// For non-partitioned projections, this option is ignored.
    /// For partitioned projections, specifying a partition key replays only that partition.
    /// 
    /// This is useful for:
    /// - Rebuilding a single corrupted partition
    /// - Testing partition-specific logic
    /// - Incremental partition replay
    /// </remarks>
    public string? Partition { get; set; }

    /// <summary>
    /// When true, processes events without persisting checkpoints.
    /// </summary>
    /// <remarks>
    /// Default: false
    /// 
    /// Dry run mode is useful for:
    /// - Testing projection logic without affecting checkpoint state
    /// - Validating replay behavior before committing
    /// - Debugging event processing without side effects
    /// 
    /// Note: Projection handlers will still execute and may modify read models.
    /// To prevent read model modifications, implement read-only logic in handlers.
    /// </remarks>
    public bool DryRun { get; set; } = false;

    /// <summary>
    /// Controls when checkpoints are written during replay.
    /// </summary>
    /// <remarks>
    /// Default: CheckpointMode.Normal
    /// 
    /// Checkpoint modes:
    /// - Normal: Write checkpoints according to BatchSize (default)
    /// - FinalOnly: Write checkpoint only after replay completes
    /// - None: Never write checkpoints (similar to DryRun for checkpoints)
    /// 
    /// FinalOnly mode reduces checkpoint write overhead during replay but provides
    /// less recovery granularity if replay is interrupted.
    /// </remarks>
    public CheckpointMode CheckpointMode { get; set; } = CheckpointMode.Normal;

    /// <summary>
    /// Validates the replay options.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when options are invalid</exception>
    public void Validate()
    {
        if (FromPosition.HasValue && FromPosition.Value < 0)
        {
            throw new InvalidOperationException(
                $"FromPosition must be non-negative. Got: {FromPosition.Value}");
        }

        if (ToPosition.HasValue && ToPosition.Value < 0)
        {
            throw new InvalidOperationException(
                $"ToPosition must be non-negative. Got: {ToPosition.Value}");
        }

        if (FromPosition.HasValue && ToPosition.HasValue && FromPosition.Value > ToPosition.Value)
        {
            throw new InvalidOperationException(
                $"FromPosition ({FromPosition.Value}) cannot be greater than ToPosition ({ToPosition.Value})");
        }

        if (BatchSize.HasValue && BatchSize.Value <= 0)
        {
            throw new InvalidOperationException(
                $"BatchSize must be positive. Got: {BatchSize.Value}");
        }
    }
}

/// <summary>
/// Controls when checkpoints are written during replay operations.
/// </summary>
public enum CheckpointMode
{
    /// <summary>
    /// Write checkpoints according to the configured BatchSize.
    /// This is the default mode, providing normal checkpoint behavior.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Write checkpoint only after the entire replay completes successfully.
    /// This reduces checkpoint write overhead during replay but provides
    /// less recovery granularity if replay is interrupted.
    /// </summary>
    FinalOnly = 1,

    /// <summary>
    /// Never write checkpoints during replay.
    /// This is useful for testing or when checkpoint management is handled externally.
    /// </summary>
    None = 2
}
