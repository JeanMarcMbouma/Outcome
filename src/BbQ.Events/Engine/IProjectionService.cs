namespace BbQ.Events.Engine;

/// <summary>
/// High-level projection service with built-in batch processing, parallel processing,
/// and automatic checkpointing.
/// 
/// The projection service provides a higher-level abstraction over the projection engine
/// that automatically handles:
/// - Collecting events into configurable batches
/// - Dispatching batches to <see cref="BbQ.Events.Projections.IProjectionBatchHandler{TEvent}"/> handlers
/// - Parallel processing across partitions with configurable concurrency
/// - Automatic checkpoint persistence after each batch
/// </summary>
/// <remarks>
/// The projection service runs as a long-lived process that continuously processes
/// events from the event bus. It complements the existing <see cref="IProjectionEngine"/>
/// by adding batch processing capabilities.
/// 
/// Key differences from IProjectionEngine:
/// - Supports batch handlers (IProjectionBatchHandler) in addition to single-event handlers
/// - Collects events into configurable batches before dispatching
/// - Provides automatic checkpointing after each batch
/// 
/// Usage:
/// <code>
/// // Register batch projections and the service
/// services.AddInMemoryEventBus();
/// services.AddBatchProjection&lt;UserProfileBatchProjection&gt;(options =&gt;
/// {
///     options.BatchSize = 50;
///     options.BatchTimeout = TimeSpan.FromSeconds(5);
///     options.MaxDegreeOfParallelism = 4;
///     options.AutoCheckpoint = true;
/// });
/// services.AddProjectionService();
/// 
/// // Run the service
/// var service = serviceProvider.GetRequiredService&lt;IProjectionService&gt;();
/// await service.RunAsync(cancellationToken);
/// </code>
/// 
/// The service can be hosted as a background service:
/// <code>
/// public class ProjectionServiceHost : BackgroundService
/// {
///     private readonly IProjectionService _service;
///     
///     public ProjectionServiceHost(IProjectionService service)
///     {
///         _service = service;
///     }
///     
///     protected override Task ExecuteAsync(CancellationToken stoppingToken)
///     {
///         return _service.RunAsync(stoppingToken);
///     }
/// }
/// </code>
/// </remarks>
public interface IProjectionService
{
    /// <summary>
    /// Runs the projection service, processing events in batches until cancelled.
    /// </summary>
    /// <param name="ct">Cancellation token to stop the service</param>
    /// <returns>A task that completes when the service stops</returns>
    /// <remarks>
    /// This method:
    /// - Subscribes to the event bus for all registered batch projection event types
    /// - Collects events into batches (by size or timeout)
    /// - Dispatches batches to registered batch handlers
    /// - Automatically saves checkpoints after each batch
    /// - Processes partitions in parallel up to MaxDegreeOfParallelism
    /// - Runs until the cancellation token is triggered
    /// - Flushes remaining events and saves final checkpoints on shutdown
    /// </remarks>
    Task RunAsync(CancellationToken ct = default);
}
