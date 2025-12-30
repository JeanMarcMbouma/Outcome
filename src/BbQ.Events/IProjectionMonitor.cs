namespace BbQ.Events;

/// <summary>
/// Interface for monitoring projection health and performance.
/// </summary>
/// <remarks>
/// Implementations of this interface track metrics about projection processing:
/// - Events processed per second
/// - Per-partition lag (current position vs latest event)
/// - Worker count
/// - Checkpoint write frequency
/// 
/// This enables monitoring systems to:
/// - Detect processing lag and backpressure
/// - Track throughput and performance  
/// - Alert on stalled or unhealthy projections
/// - Optimize checkpoint frequency and parallelism
/// 
/// The projection engine calls these methods during event processing to report
/// progress. Implementations may expose metrics via:
/// - Prometheus endpoints
/// - Application Insights
/// - CloudWatch
/// - Custom dashboards
/// 
/// Example usage with Prometheus:
/// <code>
/// public class PrometheusProjectionMonitor : IProjectionMonitor
/// {
///     private readonly Counter _eventsProcessed = Metrics.CreateCounter(
///         "projection_events_processed_total", 
///         "Total events processed by projection",
///         new CounterConfiguration { LabelNames = new[] { "projection", "partition" } });
///     
///     private readonly Gauge _lag = Metrics.CreateGauge(
///         "projection_lag", 
///         "Lag between current and latest position",
///         new GaugeConfiguration { LabelNames = new[] { "projection", "partition" } });
///     
///     public void RecordEventProcessed(string projectionName, string partitionKey, long currentPosition)
///     {
///         _eventsProcessed.WithLabels(projectionName, partitionKey).Inc();
///     }
///     
///     public void RecordLag(string projectionName, string partitionKey, long currentPosition, long? latestPosition)
///     {
///         if (latestPosition.HasValue)
///         {
///             var lag = Math.Max(0, latestPosition.Value - currentPosition);
///             _lag.WithLabels(projectionName, partitionKey).Set(lag);
///         }
///     }
/// }
/// </code>
/// </remarks>
public interface IProjectionMonitor
{
    /// <summary>
    /// Records that an event was successfully processed.
    /// </summary>
    /// <param name="projectionName">The name of the projection</param>
    /// <param name="partitionKey">The partition key</param>
    /// <param name="currentPosition">The current position after processing this event</param>
    void RecordEventProcessed(string projectionName, string partitionKey, long currentPosition);

    /// <summary>
    /// Records that a checkpoint was written.
    /// </summary>
    /// <param name="projectionName">The name of the projection</param>
    /// <param name="partitionKey">The partition key</param>
    /// <param name="position">The checkpoint position</param>
    void RecordCheckpointWritten(string projectionName, string partitionKey, long position);

    /// <summary>
    /// Records the lag between current and latest positions.
    /// </summary>
    /// <param name="projectionName">The name of the projection</param>
    /// <param name="partitionKey">The partition key</param>
    /// <param name="currentPosition">The current position</param>
    /// <param name="latestPosition">The latest available position, or null if unknown</param>
    void RecordLag(string projectionName, string partitionKey, long currentPosition, long? latestPosition);

    /// <summary>
    /// Records a change in worker count for a projection.
    /// </summary>
    /// <param name="projectionName">The name of the projection</param>
    /// <param name="workerCount">The current number of workers</param>
    void RecordWorkerCount(string projectionName, int workerCount);

    /// <summary>
    /// Records the current queue depth for a partition.
    /// </summary>
    /// <param name="projectionName">The name of the projection</param>
    /// <param name="partitionKey">The partition key</param>
    /// <param name="queueDepth">The current number of events waiting in the queue</param>
    void RecordQueueDepth(string projectionName, string partitionKey, int queueDepth);

    /// <summary>
    /// Records that an event was dropped due to backpressure.
    /// </summary>
    /// <param name="projectionName">The name of the projection</param>
    /// <param name="partitionKey">The partition key</param>
    void RecordEventDropped(string projectionName, string partitionKey);

    /// <summary>
    /// Gets the current metrics for a specific projection partition.
    /// </summary>
    /// <param name="projectionName">The name of the projection</param>
    /// <param name="partitionKey">The partition key</param>
    /// <returns>The current metrics, or null if not found</returns>
    ProjectionMetrics? GetMetrics(string projectionName, string partitionKey);

    /// <summary>
    /// Gets all current metrics for all projections and partitions.
    /// </summary>
    /// <returns>A collection of all projection metrics</returns>
    IEnumerable<ProjectionMetrics> GetAllMetrics();
}
