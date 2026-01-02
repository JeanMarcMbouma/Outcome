namespace BbQ.Events.Engine;

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
/// services.AddProjection<UserProfileProjection>(options => 
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
    /// 
    /// CURRENT BEHAVIOR: In the default implementation, this mode starts from the 
    /// beginning of the event stream (same as Replay) because determining "near-real-time" 
    /// position requires event store query capabilities not yet implemented.
    /// 
    /// INTENDED BEHAVIOR: The projection should skip to a recent position in the 
    /// event stream (near the current time) and then process events normally.
    /// This will be useful for new projections that don't need full historical data.
    /// 
    /// Note: The distinction from LiveOnly is more relevant with persistent event 
    /// stores where historical events exist.
    /// </summary>
    CatchUp = 2,

    /// <summary>
    /// Process only new events, ignoring historical events.
    /// 
    /// CURRENT BEHAVIOR: In the default implementation with InMemoryEventBus (which 
    /// doesn't persist historical events), this starts processing from the first event 
    /// received after startup, effectively achieving "live-only" behavior. With a 
    /// persistent event store, this mode currently starts from the beginning like Replay.
    /// 
    /// INTENDED BEHAVIOR: The projection should start from the current position in the 
    /// event stream, ignoring all historical events that occurred before startup.
    /// This will be useful for projections that only need to track future activity.
    /// 
    /// Note: Full implementation requires event source support for determining and 
    /// starting from "current position" in persistent event stores.
    /// </summary>
    LiveOnly = 3
}
