// -------------------------------
// Batch projection handler contracts
// -------------------------------
namespace BbQ.Events.Projections;

/// <summary>
/// Handler interface for projecting a batch of events into read models or materialized views.
/// 
/// Batch projection handlers receive multiple events at once, enabling efficient bulk
/// operations such as batch database writes, bulk upserts, or aggregated processing.
/// Unlike <see cref="IProjectionHandler{TEvent}"/> which processes events one-by-one,
/// this interface collects events into configurable batches before dispatching.
/// </summary>
/// <typeparam name="TEvent">The type of event to project</typeparam>
/// <remarks>
/// Batch projection handlers should be:
/// - Idempotent: Safe to process the same batch multiple times
/// - Side-effect free: Only update read models, no external actions
/// - Deterministic: Same batch always produces same projection result
/// - Transactional: Process the entire batch atomically when possible
/// 
/// Batch processing improves throughput by:
/// - Reducing per-event overhead (e.g., one database round-trip per batch)
/// - Enabling bulk write operations
/// - Reducing checkpoint frequency (one checkpoint per batch)
/// 
/// Example usage:
/// <code>
/// [Projection]
/// public class UserProfileBatchProjection : IProjectionBatchHandler&lt;UserCreated&gt;
/// {
///     private readonly IUserRepository _repository;
///     
///     public UserProfileBatchProjection(IUserRepository repository)
///     {
///         _repository = repository;
///     }
///     
///     public async ValueTask ProjectBatchAsync(IReadOnlyList&lt;UserCreated&gt; events, CancellationToken ct)
///     {
///         var profiles = events.Select(e =&gt; new UserProfile(e.UserId, e.Name, e.Email));
///         await _repository.BulkUpsertAsync(profiles, ct);
///     }
/// }
/// </code>
/// 
/// Registration:
/// <code>
/// services.AddBatchProjection&lt;UserProfileBatchProjection&gt;(options =&gt;
/// {
///     options.BatchSize = 50;
///     options.BatchTimeout = TimeSpan.FromSeconds(5);
/// });
/// services.AddProjectionService();
/// </code>
/// </remarks>
public interface IProjectionBatchHandler<in TEvent>
{
    /// <summary>
    /// Projects a batch of events into a read model or materialized view.
    /// </summary>
    /// <param name="events">The batch of events to project</param>
    /// <param name="ct">Cancellation token for async operations</param>
    /// <returns>A task that completes when the batch has been projected</returns>
    /// <remarks>
    /// This method is called by the projection service when a batch of events
    /// of type TEvent has been collected. The batch size is controlled by
    /// <see cref="Engine.ProjectionServiceOptions.BatchSize"/> and
    /// <see cref="Engine.ProjectionServiceOptions.BatchTimeout"/>.
    /// 
    /// Guidelines:
    /// - Must be idempotent (safe to call multiple times with the same batch)
    /// - Should process the entire batch atomically when possible
    /// - Should only update read models (no side effects like sending emails)
    /// - Use cancellation token for long-running operations
    /// - Exceptions will be logged and may cause the batch to retry
    /// </remarks>
    ValueTask ProjectBatchAsync(IReadOnlyList<TEvent> events, CancellationToken ct = default);
}
