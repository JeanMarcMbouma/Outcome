namespace BbQ.Events.Engine;

/// <summary>
/// Configuration options for projection handlers.
/// </summary>
/// <remarks>
/// These options control the behavior of projection processing including:
/// - Parallelism control via MaxDegreeOfParallelism
/// - Checkpoint batching via CheckpointBatchSize
/// - Backpressure control via ChannelCapacity and BackpressureStrategy
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

    /// <summary>
    /// Configuration for error handling during event processing.
    /// </summary>
    /// <remarks>
    /// Default: Retry with 3 attempts, exponential backoff, fallback to Skip
    /// 
    /// Controls how the projection responds to processing failures:
    /// - Retry: Retries with exponential backoff for transient failures
    /// - Skip: Logs and continues, marking event as processed
    /// - Stop: Halts projection worker for manual intervention
    /// 
    /// Configure per-projection based on business requirements and failure tolerance.
    /// </remarks>
    public ProjectionErrorHandlingOptions ErrorHandling { get; set; } = new ProjectionErrorHandlingOptions();

    /// <summary>
    /// Maximum number of events that can be queued per partition before backpressure is applied.
    /// </summary>
    /// <remarks>
    /// Default: 1000
    /// 
    /// This controls the internal queue size for each partition worker. When the queue
    /// reaches this capacity, the configured BackpressureStrategy determines what happens:
    /// - Block: Event ingestion waits until queue space is available
    /// - DropNewest: New events are discarded when queue is full
    /// - DropOldest: Oldest queued events are discarded to make room
    /// 
    /// Recommended values:
    /// - Low throughput (100-1000): For predictable, low-volume workloads
    /// - Medium throughput (1000-5000): For most production workloads
    /// - High throughput (5000-10000): For high-volume event streams
    /// 
    /// Monitor queue depth metrics to tune this value appropriately.
    /// </remarks>
    public int ChannelCapacity { get; set; } = 1000;

    /// <summary>
    /// Strategy for handling backpressure when event queue reaches capacity.
    /// </summary>
    /// <remarks>
    /// Default: BackpressureStrategy.Block
    /// 
    /// Determines behavior when ChannelCapacity is reached:
    /// - Block: Safest option, applies backpressure to event publishers (default)
    /// - DropNewest: Drops incoming events, preserves older ones (debug only)
    /// - DropOldest: Drops oldest queued events, preserves newer ones (real-time systems)
    /// 
    /// Choose based on your requirements:
    /// - Critical projections: Use Block to ensure no data loss
    /// - Debugging scenarios: Use DropNewest to inspect older events
    /// - Real-time dashboards: Use DropOldest to always show latest data
    /// 
    /// Warning: Drop strategies may cause projection state inconsistencies.
    /// Use with caution in production systems.
    /// </remarks>
    public BackpressureStrategy BackpressureStrategy { get; set; } = BackpressureStrategy.Block;
}
