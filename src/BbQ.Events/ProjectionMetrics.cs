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
/// 
/// Thread safety: This class uses internal locking to ensure thread-safe updates
/// when multiple threads modify the same metrics instance concurrently.
/// </remarks>
public class ProjectionMetrics
{
    private readonly object _lock = new();
    private long _currentPosition;
    private long? _latestEventPosition;
    private long _eventsProcessed;
    private long _checkpointsWritten;
    private DateTime? _lastCheckpointTime;
    private DateTime? _processingStartTime;
    private DateTime? _lastEventProcessedTime;
    private int _workerCount;
    private int _queueDepth;
    private int _eventsDropped;

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
    public long CurrentPosition
    {
        get { lock (_lock) return _currentPosition; }
        set { lock (_lock) _currentPosition = value; }
    }

    /// <summary>
    /// Gets or sets the latest event position available in the stream.
    /// </summary>
    /// <remarks>
    /// This value may be null if the stream position cannot be determined.
    /// </remarks>
    public long? LatestEventPosition
    {
        get { lock (_lock) return _latestEventPosition; }
        set { lock (_lock) _latestEventPosition = value; }
    }

    /// <summary>
    /// Gets the lag between the current position and the latest event position.
    /// </summary>
    /// <remarks>
    /// Lag = LatestEventPosition - CurrentPosition
    /// A high lag indicates the projection is falling behind.
    /// Returns 0 if LatestEventPosition is null or if the projection is caught up.
    /// </remarks>
    public long Lag
    {
        get
        {
            lock (_lock)
            {
                return _latestEventPosition.HasValue 
                    ? Math.Max(0, _latestEventPosition.Value - _currentPosition) 
                    : 0;
            }
        }
    }

    /// <summary>
    /// Gets or sets the total number of events processed since startup.
    /// </summary>
    public long EventsProcessed
    {
        get { lock (_lock) return _eventsProcessed; }
        set { lock (_lock) _eventsProcessed = value; }
    }

    /// <summary>
    /// Gets or sets the total number of checkpoints written since startup.
    /// </summary>
    public long CheckpointsWritten
    {
        get { lock (_lock) return _checkpointsWritten; }
        set { lock (_lock) _checkpointsWritten = value; }
    }

    /// <summary>
    /// Gets or sets the timestamp when the last checkpoint was written.
    /// </summary>
    public DateTime? LastCheckpointTime
    {
        get { lock (_lock) return _lastCheckpointTime; }
        set { lock (_lock) _lastCheckpointTime = value; }
    }

    /// <summary>
    /// Gets or sets the timestamp when the projection started processing (first event).
    /// </summary>
    public DateTime? ProcessingStartTime
    {
        get { lock (_lock) return _processingStartTime; }
        set { lock (_lock) _processingStartTime = value; }
    }

    /// <summary>
    /// Gets or sets the timestamp when the last event was processed.
    /// </summary>
    public DateTime? LastEventProcessedTime
    {
        get { lock (_lock) return _lastEventProcessedTime; }
        set { lock (_lock) _lastEventProcessedTime = value; }
    }

    /// <summary>
    /// Gets or sets the number of active workers for this projection.
    /// </summary>
    /// <remarks>
    /// For partitioned projections with MaxDegreeOfParallelism > 1, this represents
    /// the number of concurrent partition workers processing different partitions.
    /// Worker count grows as new partitions are discovered and remains constant
    /// until the projection engine shuts down, as partition workers are long-lived.
    /// </remarks>
    public int WorkerCount
    {
        get { lock (_lock) return _workerCount; }
        set { lock (_lock) _workerCount = value; }
    }

    /// <summary>
    /// Gets or sets the current queue depth (number of events waiting to be processed).
    /// </summary>
    /// <remarks>
    /// This metric indicates how many events are currently buffered in the partition
    /// worker's channel waiting to be processed. A consistently high queue depth
    /// indicates backpressure - the projection is falling behind event ingestion.
    /// 
    /// Use this metric to:
    /// - Detect processing bottlenecks
    /// - Tune ChannelCapacity and MaxDegreeOfParallelism
    /// - Alert on queue saturation (approaching ChannelCapacity)
    /// - Identify when to scale up processing resources
    /// 
    /// A healthy system typically has low queue depth (0-10% of capacity).
    /// High queue depth (>50% of capacity) warrants investigation.
    /// </remarks>
    public int QueueDepth
    {
        get { lock (_lock) return _queueDepth; }
        set { lock (_lock) _queueDepth = value; }
    }

    /// <summary>
    /// Gets or sets the total number of events dropped due to backpressure.
    /// </summary>
    /// <remarks>
    /// This counter is incremented when events are dropped because the queue
    /// reached capacity and the BackpressureStrategy is set to DropNewest or DropOldest.
    /// 
    /// A non-zero value indicates:
    /// - Processing cannot keep pace with ingestion
    /// - Data loss is occurring
    /// - ChannelCapacity may need to be increased
    /// - MaxDegreeOfParallelism may need to be tuned
    /// - Consider switching to Block strategy if data loss is unacceptable
    /// 
    /// This metric is critical for monitoring data integrity in projections.
    /// </remarks>
    public int EventsDropped
    {
        get { lock (_lock) return _eventsDropped; }
        set { lock (_lock) _eventsDropped = value; }
    }

    /// <summary>
    /// Gets the events processed per second based on total processing time.
    /// </summary>
    /// <remarks>
    /// This is calculated as EventsProcessed / (time elapsed since first event).
    /// Returns 0 if no events have been processed or if ProcessingStartTime is null.
    /// The calculation provides average throughput over the lifetime of the projection.
    /// </remarks>
    public double EventsPerSecond
    {
        get
        {
            lock (_lock)
            {
                if (!_processingStartTime.HasValue || _eventsProcessed == 0)
                    return 0;

                var elapsed = DateTime.UtcNow - _processingStartTime.Value;
                return elapsed.TotalSeconds > 0 
                    ? _eventsProcessed / elapsed.TotalSeconds 
                    : 0;
            }
        }
    }

    /// <summary>
    /// Atomically increments the events processed counter and updates the last event time.
    /// </summary>
    internal void IncrementEventsProcessed()
    {
        lock (_lock)
        {
            _eventsProcessed++;
            _lastEventProcessedTime = DateTime.UtcNow;
            
            // Set start time on first event
            if (!_processingStartTime.HasValue)
            {
                _processingStartTime = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Atomically increments the checkpoints written counter and updates the last checkpoint time.
    /// </summary>
    internal void IncrementCheckpointsWritten()
    {
        lock (_lock)
        {
            _checkpointsWritten++;
            _lastCheckpointTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Atomically updates the position and latest event position.
    /// </summary>
    internal void UpdatePositions(long currentPosition, long? latestPosition)
    {
        lock (_lock)
        {
            _currentPosition = currentPosition;
            _latestEventPosition = latestPosition;
        }
    }

    /// <summary>
    /// Atomically increments the events dropped counter.
    /// </summary>
    internal void IncrementEventsDropped()
    {
        lock (_lock)
        {
            _eventsDropped++;
        }
    }
}
