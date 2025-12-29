namespace BbQ.Events;

/// <summary>
/// Configuration options for projection handlers.
/// </summary>
/// <remarks>
/// These options control the behavior of projection processing including:
/// - Parallelism control via MaxDegreeOfParallelism
/// - Checkpoint batching via CheckpointBatchSize
/// 
/// Options can be specified via the [Projection] attribute or configured
/// globally for all projections.
/// </remarks>
public class ProjectionOptions
{
    /// <summary>
    /// Maximum number of partitions that can be processed in parallel for this projection.
    /// </summary>
    /// <remarks>
    /// Default: 1 (sequential processing)
    /// 
    /// This limits the number of concurrent partition workers:
    /// - 1: All partitions processed sequentially
    /// - N: Up to N partitions processed in parallel
    /// - 0 or negative: Unlimited parallelism (use with caution)
    /// 
    /// Within each partition, events are always processed sequentially to maintain ordering.
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
}
