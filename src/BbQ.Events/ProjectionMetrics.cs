namespace BbQ.Events;

/// <summary>
/// Represents metrics for a single projection or partition.
/// </summary>
/// <remarks>
/// This class provides detailed observability into projection health and performance:
/// - CurrentPosition: Where the projection has processed up to
/// - LatestEventPosition: The highest event position available
/// - Lag: How far behind the projection is (LatestEventPosition - CurrentPosition)
/// - EventsProcessed: Total events processed since startup
/// - CheckpointsWritten: Total checkpoints persisted since startup
/// - LastCheckpointTime: When the last checkpoint was written
/// - LastEventProcessedTime: When the last event was processed
/// - WorkerCount: Number of active workers for this projection
/// 
/// These metrics enable monitoring systems to:
/// - Detect processing lag and backpressure
/// - Track throughput and performance
/// - Alert on stalled or unhealthy projections
/// - Optimize checkpoint frequency and parallelism
/// </remarks>
public class ProjectionMetrics
{
    /// <summary>
    /// Gets or sets the name of the projection.
    /// </summary>
    public string ProjectionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the partition key (for partitioned projections).
    /// </summary>
    /// <remarks>
    /// For non-partitioned projections, this is typically "_default".
    /// </remarks>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current position in the event stream that this projection has processed.
    /// </summary>
    public long CurrentPosition { get; set; }

    /// <summary>
    /// Gets or sets the latest event position available in the stream.
    /// </summary>
    /// <remarks>
    /// This value may be null if the stream position cannot be determined.
    /// </remarks>
    public long? LatestEventPosition { get; set; }

    /// <summary>
    /// Gets the lag between the current position and the latest event position.
    /// </summary>
    /// <remarks>
    /// Lag = LatestEventPosition - CurrentPosition
    /// A high lag indicates the projection is falling behind.
    /// Returns 0 if LatestEventPosition is null or if the projection is caught up.
    /// </remarks>
    public long Lag => LatestEventPosition.HasValue 
        ? Math.Max(0, LatestEventPosition.Value - CurrentPosition) 
        : 0;

    /// <summary>
    /// Gets or sets the total number of events processed since startup.
    /// </summary>
    public long EventsProcessed { get; set; }

    /// <summary>
    /// Gets or sets the total number of checkpoints written since startup.
    /// </summary>
    public long CheckpointsWritten { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the last checkpoint was written.
    /// </summary>
    public DateTime? LastCheckpointTime { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the last event was processed.
    /// </summary>
    public DateTime? LastEventProcessedTime { get; set; }

    /// <summary>
    /// Gets or sets the number of active workers for this projection.
    /// </summary>
    /// <remarks>
    /// For partitioned projections with MaxDegreeOfParallelism > 1, this represents
    /// the number of concurrent workers processing different partitions.
    /// </remarks>
    public int WorkerCount { get; set; }

    /// <summary>
    /// Gets the events processed per second based on recent activity.
    /// </summary>
    /// <remarks>
    /// This is calculated as EventsProcessed / (time since startup or last reset).
    /// Returns 0 if no events have been processed or if LastEventProcessedTime is null.
    /// </remarks>
    public double EventsPerSecond
    {
        get
        {
            if (!LastEventProcessedTime.HasValue || EventsProcessed == 0)
                return 0;

            var elapsed = DateTime.UtcNow - LastEventProcessedTime.Value;
            return elapsed.TotalSeconds > 0 
                ? EventsProcessed / elapsed.TotalSeconds 
                : 0;
        }
    }
}
