namespace BbQ.Events;

/// <summary>
/// Orchestrates the execution of projection handlers by subscribing to event streams
/// and dispatching events to handlers.
/// 
/// The projection engine is the runtime component that:
/// - Subscribes to the event bus for configured event types
/// - Routes events to registered projection handlers
/// - Handles errors gracefully and continues processing
/// - Provides infrastructure for checkpointing (via IProjectionCheckpointStore)
/// </summary>
/// <remarks>
/// The projection engine runs as a background service that continuously processes
/// events from live event streams. The default implementation:
/// - Processes events sequentially (not parallel)
/// - Does not implement automatic checkpointing (infrastructure provided)
/// - Logs progress and errors
/// 
/// For production workloads, consider implementing:
/// - Custom checkpoint logic using IProjectionCheckpointStore
/// - Parallel processing for partitioned projections
/// - Retry policies and dead-letter queues
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
    /// - Dispatches events to registered projection handlers sequentially
    /// - Runs until the cancellation token is triggered
    /// - Handles errors by logging and continuing to process other events
    /// 
    /// The default engine processes events from live streams as they arrive.
    /// Checkpointing and replay functionality can be implemented via custom
    /// IProjectionEngine implementations using IProjectionCheckpointStore.
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
