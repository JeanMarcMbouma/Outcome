namespace BbQ.Events;

/// <summary>
/// Defines how projection errors should be handled during event processing.
/// </summary>
public enum ProjectionErrorHandlingStrategy
{
    /// <summary>
    /// Retry processing with exponential backoff.
    /// Failed events will be retried up to the configured maximum attempts.
    /// </summary>
    Retry,
    
    /// <summary>
    /// Skip the failed event, log the error, and continue processing.
    /// The event will be marked as processed and checkpointed.
    /// </summary>
    Skip,
    
    /// <summary>
    /// Stop the projection worker when an error occurs.
    /// The projection will need to be manually restarted.
    /// </summary>
    Stop
}
