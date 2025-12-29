namespace BbQ.Events;

/// <summary>
/// Orchestrates the execution of projection handlers by subscribing to event streams,
/// dispatching events to handlers, and maintaining checkpoints.
/// 
/// The projection engine is the runtime component that:
/// - Subscribes to the event bus for configured event types
/// - Routes events to registered projection handlers
/// - Maintains processing checkpoints for resumability
/// - Supports parallel processing for partitioned projections
/// - Handles errors and retries
/// </summary>
/// <remarks>
/// The projection engine runs as a background service that continuously processes
/// events from the event stream. It is designed to be:
/// - Resilient: Handles failures gracefully and supports retries
/// - Resumable: Uses checkpoints to resume from the last processed position
/// - Performant: Supports parallel processing of independent events
/// - Observable: Logs progress and errors
/// 
/// Usage:
/// <code>
/// // Register projections and engine
/// services.AddInMemoryEventBus();
/// services.AddProjection&lt;UserProfileProjection&gt;();
/// services.AddProjectionEngine();
/// 
/// // Run the engine (typically in a background service)
/// var engine = serviceProvider.GetRequiredService&lt;IProjectionEngine&gt;();
/// await engine.RunAsync(cancellationToken);
/// </code>
/// 
/// The engine can be hosted as a background service:
/// <code>
/// public class ProjectionHostedService : BackgroundService
/// {
///     private readonly IProjectionEngine _engine;
///     
///     public ProjectionHostedService(IProjectionEngine engine)
///     {
///         _engine = engine;
///     }
///     
///     protected override Task ExecuteAsync(CancellationToken stoppingToken)
///     {
///         return _engine.RunAsync(stoppingToken);
///     }
/// }
/// </code>
/// </remarks>
public interface IProjectionEngine
{
    /// <summary>
    /// Runs the projection engine, processing events until cancelled.
    /// </summary>
    /// <param name="ct">Cancellation token to stop the engine</param>
    /// <returns>A task that completes when the engine stops</returns>
    /// <remarks>
    /// This method:
    /// - Subscribes to the event bus for all registered projection event types
    /// - Dispatches events to registered projection handlers
    /// - Maintains checkpoints after processing batches of events
    /// - Runs until the cancellation token is triggered
    /// - Handles errors by logging and optionally retrying
    /// 
    /// The engine processes events continuously in a loop. For partitioned projections,
    /// events with different partition keys may be processed concurrently.
    /// 
    /// Example:
    /// <code>
    /// var cts = new CancellationTokenSource();
    /// var engineTask = engine.RunAsync(cts.Token);
    /// 
    /// // Run for some time
    /// await Task.Delay(TimeSpan.FromMinutes(5));
    /// 
    /// // Stop the engine gracefully
    /// cts.Cancel();
    /// await engineTask;
    /// </code>
    /// </remarks>
    Task RunAsync(CancellationToken ct = default);
}
