namespace BbQ.Events;

/// <summary>
/// Configuration options for projection handlers.
/// </summary>
/// <remarks>
/// These options control the behavior of projection processing including:
/// - Parallelism control via MaxDegreeOfParallelism
/// - Checkpoint batching via CheckpointBatchSize
/// 
/// Options can be specified via the [Projection] attribute when using source generators,
/// or configured programmatically when manually registering projections.
/// </remarks>
public class ProjectionOptions
{
    /// <summary>
    /// Maximum number of partitions that can be processed in parallel for this projection.
    /// </summary>
    /// <remarks>
    /// Default: 1 (sequential processing)
    /// 
    /// This value limits the number of concurrent event processing operations:
    /// - 1: Effectively sequential processing
    /// - N (&gt; 1): Up to N events may be processed concurrently across different partitions.
    ///   Within each partition, events are always processed sequentially.
    /// - 0 or negative: Capped at 1000 concurrent operations
    /// 
    /// The semaphore is acquired per-event, not per-worker, so this accurately controls
    /// concurrent processing load.
    /// </remarks>
    public int MaxDegreeOfParallelism { get; set; } = 1;

    /// <summary>
    /// Number of events to process before persisting a checkpoint.
    /// </summary>
    /// <remarks>
    /// Default: 100
    /// 
    /// Checkpoints are persisted after processing this many events within a partition.
    /// Smaller values provide better recovery granularity but increase checkpoint overhead.
    /// Larger values reduce checkpoint overhead but may require reprocessing more events on restart.
    /// 
    /// Set to 1 for checkpoint-after-every-event (highest durability, lowest performance).
    /// Set to higher values for better performance with acceptable replay on restart.
    /// </remarks>
    public int CheckpointBatchSize { get; set; } = 100;

    /// <summary>
    /// Gets the unique name for this projection used in checkpoint storage.
    /// </summary>
    /// <remarks>
    /// This is set automatically by the engine based on the projection handler type name.
    /// </remarks>
    public string ProjectionName { get; set; } = string.Empty;

    /// <summary>
    /// Defines how the projection should start processing events.
    /// </summary>
    /// <remarks>
    /// Default: ProjectionStartupMode.Resume
    /// 
    /// Startup modes:
    /// - Resume: Continue from the last checkpoint (default)
    /// - Replay: Rebuild from the beginning, ignoring checkpoints
    /// - CatchUp: Fast-forward to near-real-time, then switch to live
    /// - LiveOnly: Process only new events, ignoring historical events
    /// 
    /// This setting is evaluated when the projection engine starts up.
    /// Changing it after startup requires restarting the engine.
    /// </remarks>
    public ProjectionStartupMode StartupMode { get; set; } = ProjectionStartupMode.Resume;
}
