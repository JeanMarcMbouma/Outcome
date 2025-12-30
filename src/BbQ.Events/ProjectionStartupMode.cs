namespace BbQ.Events;

/// <summary>
/// Defines how a projection should start processing events.
/// </summary>
/// <remarks>
/// Startup modes control the projection's behavior when it starts or restarts:
/// - Resume: Continue from the last checkpoint (default)
/// - Replay: Rebuild from the beginning, ignoring any checkpoint
/// - CatchUp: Fast-forward to near-real-time, then switch to live processing
/// - LiveOnly: Process only new events, ignoring historical events
/// 
/// Usage:
/// <code>
/// services.AddProjection&lt;UserProfileProjection&gt;(options =&gt; 
/// {
///     options.StartupMode = ProjectionStartupMode.Replay;
/// });
/// </code>
/// </remarks>
public enum ProjectionStartupMode
{
    /// <summary>
    /// Resume processing from the last checkpoint.
    /// This is the default mode. The projection will load its checkpoint
    /// and continue processing events from where it left off.
    /// </summary>
    Resume = 0,

    /// <summary>
    /// Rebuild the projection from scratch.
    /// The projection will ignore any existing checkpoint and start
    /// processing from the beginning of the event stream.
    /// This is useful for rebuilding projections after schema changes
    /// or for recovering from corrupted projection state.
    /// </summary>
    Replay = 1,

    /// <summary>
    /// Fast-forward to near-real-time, then switch to live processing.
    /// The projection will skip to a recent position in the event stream
    /// (near the current time) and then process events normally.
    /// This is useful for new projections that don't need full historical data.
    /// </summary>
    CatchUp = 2,

    /// <summary>
    /// Process only new events, ignoring historical events.
    /// The projection will start from the current position in the event stream,
    /// ignoring all historical events that occurred before startup.
    /// This is useful for projections that only need to track future activity.
    /// </summary>
    LiveOnly = 3
}
