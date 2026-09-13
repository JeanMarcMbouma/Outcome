namespace BbQ.Events.Engine;

/// <summary>
/// Defines how projection errors should be handled during event processing.
/// </summary>
public enum ProjectionErrorHandlingStrategy
{
    /// <summary>
    /// Retry processing with exponential backoff.
    /// Failed events will be retried up to the configured maximum attempts.
    /// </summary>
    Retry = 0,

    /// <summary>Persist a durable dead letter before advancing the checkpoint.</summary>
    Quarantine = 3,
    
    /// <summary>
    /// Skip the failed event, log the error, and continue processing.
    /// The event is checkpointed with a distinct skipped disposition.
    /// </summary>
    Skip = 1,
    
    /// <summary>
    /// Stop the projection worker when an error occurs.
    /// The projection will need to be manually restarted.
    /// </summary>
    Stop = 2
}
