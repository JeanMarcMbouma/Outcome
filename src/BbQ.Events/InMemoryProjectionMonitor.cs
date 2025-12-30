using System.Collections.Concurrent;

namespace BbQ.Events;

/// <summary>
/// In-memory implementation of IProjectionMonitor for tracking projection metrics.
/// </summary>
/// <remarks>
/// This implementation:
/// - Stores metrics in memory (lost on restart)
/// - Thread-safe for concurrent updates
/// - Suitable for single-instance applications
/// - Can be used as a base for custom implementations
/// 
/// For distributed systems or persistent metrics, consider implementing:
/// - PrometheusProjectionMonitor (exports to Prometheus)
/// - ApplicationInsightsProjectionMonitor (exports to Azure)
/// - CloudWatchProjectionMonitor (exports to AWS)
/// 
/// Example usage:
/// <code>
/// services.AddSingleton&lt;IProjectionMonitor, InMemoryProjectionMonitor&gt;();
/// 
/// // Query metrics
/// var monitor = serviceProvider.GetRequiredService&lt;IProjectionMonitor&gt;();
/// var metrics = monitor.GetMetrics("UserProjection", "_default");
/// Console.WriteLine($"Lag: {metrics?.Lag}, Events/sec: {metrics?.EventsPerSecond}");
/// </code>
/// </remarks>
public class InMemoryProjectionMonitor : IProjectionMonitor
{
    private readonly ConcurrentDictionary<string, ProjectionMetrics> _metrics = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _partitionsByProjection = new();

    /// <summary>
    /// Records that an event was successfully processed.
    /// </summary>
    public void RecordEventProcessed(string projectionName, string partitionKey, long currentPosition)
    {
        var key = GetKey(projectionName, partitionKey);
        var metrics = _metrics.GetOrAdd(key, _ =>
        {
            // Track partition for this projection (using ConcurrentDictionary as a Set to avoid duplicates)
            var partitions = _partitionsByProjection.GetOrAdd(projectionName, _ => new ConcurrentDictionary<string, byte>());
            partitions.TryAdd(key, 0);
            
            return new ProjectionMetrics
            {
                ProjectionName = projectionName,
                PartitionKey = partitionKey
            };
        });

        metrics.CurrentPosition = currentPosition;
        metrics.IncrementEventsProcessed();
    }

    /// <summary>
    /// Records that a checkpoint was written.
    /// </summary>
    public void RecordCheckpointWritten(string projectionName, string partitionKey, long position)
    {
        var key = GetKey(projectionName, partitionKey);
        var metrics = _metrics.GetOrAdd(key, _ => new ProjectionMetrics
        {
            ProjectionName = projectionName,
            PartitionKey = partitionKey
        });

        metrics.IncrementCheckpointsWritten();
    }

    /// <summary>
    /// Records the lag between current and latest positions.
    /// </summary>
    public void RecordLag(string projectionName, string partitionKey, long currentPosition, long? latestPosition)
    {
        var key = GetKey(projectionName, partitionKey);
        var metrics = _metrics.GetOrAdd(key, _ => new ProjectionMetrics
        {
            ProjectionName = projectionName,
            PartitionKey = partitionKey
        });

        metrics.UpdatePositions(currentPosition, latestPosition);
    }

    /// <summary>
    /// Records a change in worker count for a projection.
    /// </summary>
    public void RecordWorkerCount(string projectionName, int workerCount)
    {
        // Update all tracked partitions for this projection efficiently
        if (_partitionsByProjection.TryGetValue(projectionName, out var partitionKeys))
        {
            foreach (var key in partitionKeys.Keys.Where(k => _metrics.ContainsKey(k)))
            {
                if (_metrics.TryGetValue(key, out var metrics))
                {
                    metrics.WorkerCount = workerCount;
                }
            }
        }
    }

    /// <summary>
    /// Records the current queue depth for a partition.
    /// </summary>
    public void RecordQueueDepth(string projectionName, string partitionKey, int queueDepth)
    {
        var key = GetKey(projectionName, partitionKey);
        var metrics = _metrics.GetOrAdd(key, _ => new ProjectionMetrics
        {
            ProjectionName = projectionName,
            PartitionKey = partitionKey
        });

        metrics.QueueDepth = queueDepth;
    }

    /// <summary>
    /// Records that an event was dropped due to backpressure.
    /// </summary>
    public void RecordEventDropped(string projectionName, string partitionKey)
    {
        var key = GetKey(projectionName, partitionKey);
        var metrics = _metrics.GetOrAdd(key, _ => new ProjectionMetrics
        {
            ProjectionName = projectionName,
            PartitionKey = partitionKey
        });

        metrics.IncrementEventsDropped();
    }

    /// <summary>
    /// Gets the current metrics for a specific projection partition.
    /// </summary>
    public ProjectionMetrics? GetMetrics(string projectionName, string partitionKey)
    {
        var key = GetKey(projectionName, partitionKey);
        return _metrics.TryGetValue(key, out var metrics) ? metrics : null;
    }

    /// <summary>
    /// Gets all current metrics for all projections and partitions.
    /// </summary>
    public IEnumerable<ProjectionMetrics> GetAllMetrics()
    {
        return _metrics.Values.ToList();
    }

    private static string GetKey(string projectionName, string partitionKey)
    {
        return $"{projectionName}:{partitionKey}";
    }
}
