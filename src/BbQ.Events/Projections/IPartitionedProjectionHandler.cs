// -------------------------------
// Projection handler contracts
// -------------------------------
namespace BbQ.Events.Projections;

/// <summary>
/// Handler interface for projecting events with partitioning support.
/// 
/// Partitioned projections enable parallel processing of events by routing them
/// to different processing partitions based on a partition key. This is useful
/// for high-throughput scenarios where events can be processed independently.
/// </summary>
/// <typeparam name="TEvent">The type of event to project</typeparam>
/// <remarks>
/// Partitioned projections provide:
/// - Parallelism: Events with different partition keys can be processed concurrently
/// - Ordering: Events with the same partition key are processed in order
/// - Scalability: Multiple projection instances can process different partitions
/// 
/// Choose a partition key that:
/// - Ensures events that must be ordered share the same key
/// - Distributes load evenly across partitions
/// - Is deterministic (same event always produces same key)
/// 
/// Common partition key strategies:
/// - Aggregate ID: Process all events for an entity in order
/// - User ID: Process all events for a user in order
/// - Region/Tenant: Distribute by geographic or organizational boundaries
/// 
/// Example usage:
/// <code>
/// [Projection]
/// public class UserStatisticsProjection : IPartitionedProjectionHandler&lt;UserActivity&gt;
/// {
///     private readonly IUserStatsRepository _repository;
///     
///     public UserStatisticsProjection(IUserStatsRepository repository)
///     {
///         _repository = repository;
///     }
///     
///     public string GetPartitionKey(UserActivity evt)
///     {
///         // Partition by user ID to ensure all events for a user are processed in order
///         return evt.UserId.ToString();
///     }
///     
///     public async ValueTask ProjectAsync(UserActivity evt, CancellationToken ct)
///     {
///         var stats = await _repository.GetByUserIdAsync(evt.UserId, ct);
///         stats.IncrementActivityCount();
///         stats.LastActivityAt = evt.Timestamp;
///         await _repository.UpsertAsync(stats, ct);
///     }
/// }
/// </code>
/// </remarks>
public interface IPartitionedProjectionHandler<in TEvent>
{
    /// <summary>
    /// Gets the partition key for routing the event to a processing partition.
    /// </summary>
    /// <param name="event">The event to get the partition key for</param>
    /// <returns>A string partition key used to route the event</returns>
    /// <remarks>
    /// The partition key:
    /// - Must be deterministic (same event always returns same key)
    /// - Should distribute load evenly across partitions
    /// - Groups events that must be processed in order
    /// 
    /// Events with the same partition key are processed sequentially in the
    /// order they appear in the event stream. Events with different partition
    /// keys may be processed concurrently.
    /// </remarks>
    string GetPartitionKey(TEvent @event);

    /// <summary>
    /// Projects the event into a read model or materialized view.
    /// </summary>
    /// <param name="event">The event to project</param>
    /// <param name="ct">Cancellation token for async operations</param>
    /// <returns>A task that completes when the projection has been applied</returns>
    /// <remarks>
    /// This method is called by the projection engine when an event of type TEvent
    /// is processed from the event stream. The engine ensures that events with the
    /// same partition key are processed sequentially in order.
    /// 
    /// Guidelines:
    /// - Must be idempotent (safe to call multiple times with the same event)
    /// - Should only update read models (no side effects like sending emails)
    /// - Should handle missing dependencies gracefully
    /// - Use cancellation token for long-running operations
    /// - Exceptions will be logged and may cause the projection to retry
    /// </remarks>
    ValueTask ProjectAsync(TEvent @event, CancellationToken ct = default);
}
