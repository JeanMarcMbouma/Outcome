namespace BbQ.Events;

/// <summary>
/// Configuration options for handling errors during projection event processing.
/// </summary>
/// <remarks>
/// These options control how the projection engine responds to failures:
/// - Retry with exponential backoff for transient failures
/// - Skip events that cannot be processed
/// - Stop projection on critical failures
/// 
/// Error handling can be configured per-projection to match business requirements.
/// </remarks>
public class ProjectionErrorHandlingOptions
{
    /// <summary>
    /// The strategy to use when an error occurs during event processing.
    /// </summary>
    /// <remarks>
    /// Default: Retry
    /// 
    /// Choose based on your requirements:
    /// - Retry: Best for transient failures (network issues, temporary unavailability)
    /// - Skip: Best when event processing failures should not block the projection
    /// - Stop: Best when data consistency is critical and manual intervention is required
    /// </remarks>
    public ProjectionErrorHandlingStrategy Strategy { get; set; } = ProjectionErrorHandlingStrategy.Retry;
    
    /// <summary>
    /// Maximum number of retry attempts before giving up (for Retry strategy).
    /// </summary>
    /// <remarks>
    /// Default: 3
    /// 
    /// After exhausting retries, the behavior depends on FallbackStrategy:
    /// - If FallbackStrategy is Skip, the event is skipped and processing continues
    /// - If FallbackStrategy is Stop, the projection stops
    /// </remarks>
    public int MaxRetryAttempts { get; set; } = 3;
    
    /// <summary>
    /// Initial delay in milliseconds before the first retry (for Retry strategy).
    /// </summary>
    /// <remarks>
    /// Default: 1000ms (1 second)
    /// 
    /// Subsequent retries use exponential backoff:
    /// - Retry 1: InitialRetryDelayMs
    /// - Retry 2: InitialRetryDelayMs * 2
    /// - Retry 3: InitialRetryDelayMs * 4
    /// And so on, up to MaxRetryDelayMs
    /// </remarks>
    public int InitialRetryDelayMs { get; set; } = 1000;
    
    /// <summary>
    /// Maximum delay in milliseconds between retry attempts (for Retry strategy).
    /// </summary>
    /// <remarks>
    /// Default: 30000ms (30 seconds)
    /// 
    /// Caps the exponential backoff to prevent excessively long delays.
    /// </remarks>
    public int MaxRetryDelayMs { get; set; } = 30000;
    
    /// <summary>
    /// Strategy to use after all retry attempts are exhausted (for Retry strategy).
    /// </summary>
    /// <remarks>
    /// Default: Skip
    /// 
    /// Options:
    /// - Skip: Continue processing after logging the failure
    /// - Stop: Stop the projection worker for manual intervention
    /// </remarks>
    public ProjectionErrorHandlingStrategy FallbackStrategy { get; set; } = ProjectionErrorHandlingStrategy.Skip;
}
