namespace BbQ.Events.Engine;

/// <summary>
/// Configuration options for the projection service.
/// </summary>
/// <remarks>
/// These options control the behavior of the projection service including:
/// - Batch processing via BatchSize and BatchTimeout
/// - Parallelism control via MaxDegreeOfParallelism
/// - Automatic checkpointing via AutoCheckpoint and CheckpointAfterEachBatch
/// - Error handling via ErrorHandling
/// 
/// Options can be configured programmatically when registering batch projections:
/// <code>
/// services.AddBatchProjection&lt;MyProjection&gt;(options =&gt;
/// {
///     options.BatchSize = 50;
///     options.BatchTimeout = TimeSpan.FromSeconds(5);
///     options.MaxDegreeOfParallelism = 4;
/// });
/// </code>
/// </remarks>
public class ProjectionServiceOptions
{
    /// <summary>
    /// Maximum number of events to collect before dispatching as a batch.
    /// </summary>
    /// <remarks>
    /// Default: 100
    /// 
    /// The service collects events until either:
    /// - This batch size is reached, or
    /// - The BatchTimeout expires (whichever comes first)
    /// 
    /// Larger batches improve throughput but increase latency and memory usage.
    /// Smaller batches reduce latency but may increase per-event overhead.
    /// 
    /// Set to 1 for event-at-a-time processing (equivalent to IProjectionHandler).
    /// </remarks>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Maximum time to wait for a full batch before dispatching a partial batch.
    /// </summary>
    /// <remarks>
    /// Default: 5 seconds
    /// 
    /// When events arrive slowly, this timeout ensures partial batches are
    /// dispatched within a bounded time. This prevents events from sitting
    /// in the buffer indefinitely when the stream is idle.
    /// 
    /// Set to <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> to
    /// wait indefinitely for a full batch (not recommended for production).
    /// </remarks>
    public TimeSpan BatchTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum number of concurrent batch processing operations.
    /// </summary>
    /// <remarks>
    /// Default: 1 (sequential processing)
    /// 
    /// This value limits the number of concurrent batch processing operations:
    /// - 1: Sequential batch processing
    /// - N (&gt; 1): Up to N batches may be processed concurrently
    /// - 0 or negative: Capped at DefaultMaxConcurrentBatches (1000) concurrent operations
    /// 
    /// When used with partitioned batch handlers, each partition collects
    /// its own batch independently and processes in parallel up to this limit.
    /// </remarks>
    public int MaxDegreeOfParallelism { get; set; } = 1;

    /// <summary>
    /// Whether to automatically save checkpoints after each batch is processed.
    /// </summary>
    /// <remarks>
    /// Default: true
    /// 
    /// When enabled, the service automatically saves a checkpoint after each batch
    /// is successfully processed. This provides at-least-once delivery semantics.
    /// 
    /// Disable this if you want to manage checkpoints manually or if you
    /// need exactly-once semantics with custom checkpoint management.
    /// </remarks>
    public bool AutoCheckpoint { get; set; } = true;

    /// <summary>
    /// Gets the unique name for this projection used in checkpoint storage.
    /// </summary>
    /// <remarks>
    /// This is set automatically based on the projection handler type name.
    /// </remarks>
    public string ProjectionName { get; set; } = string.Empty;

    /// <summary>
    /// Configuration for error handling during batch processing.
    /// </summary>
    /// <remarks>
    /// Default: Retry with 3 attempts, exponential backoff, fallback to Skip
    /// 
    /// Controls how the service responds to batch processing failures:
    /// - Retry: Retries the entire batch with exponential backoff
    /// - Skip: Logs and continues, marking the batch as processed
    /// - Stop: Halts the service for manual intervention
    /// </remarks>
    public ProjectionErrorHandlingOptions ErrorHandling { get; set; } = new ProjectionErrorHandlingOptions();

    /// <summary>
    /// Maximum number of events that can be queued before backpressure is applied.
    /// </summary>
    /// <remarks>
    /// Default: 1000
    /// 
    /// Controls the internal queue size. When the queue reaches this capacity,
    /// event ingestion blocks until space is available.
    /// </remarks>
    public int ChannelCapacity { get; set; } = 1000;

    /// <summary>
    /// Defines how the projection should start processing events.
    /// </summary>
    /// <remarks>
    /// Default: ProjectionStartupMode.Resume
    /// </remarks>
    public ProjectionStartupMode StartupMode { get; set; } = ProjectionStartupMode.Resume;
}
