// -------------------------------
// Projection handler contracts
// -------------------------------
namespace BbQ.Events;

/// <summary>
/// Handler interface for projecting events into read models or materialized views.
/// 
/// Projection handlers transform events into queryable state, enabling efficient
/// read-side operations in event-sourced systems. Unlike event handlers which are
/// invoked immediately when events are published, projections are typically run
/// by a projection engine that processes event streams in a controlled manner.
/// </summary>
/// <typeparam name="TEvent">The type of event to project</typeparam>
/// <remarks>
/// Projection handlers should be:
/// - Idempotent: Safe to process the same event multiple times
/// - Side-effect free: Only update read models, no external actions
/// - Deterministic: Same event always produces same projection result
/// 
/// Projections are discovered and registered using the [Projection] attribute
/// and can be run automatically by the projection engine.
/// 
/// Example usage:
/// <code>
/// [Projection]
/// public class UserProfileProjection :
///     IProjectionHandler&lt;UserCreated&gt;,
///     IProjectionHandler&lt;UserUpdated&gt;
/// {
///     private readonly IUserReadRepository _repository;
///     
///     public UserProfileProjection(IUserReadRepository repository)
///     {
///         _repository = repository;
///     }
///     
///     public async ValueTask ProjectAsync(UserCreated evt, CancellationToken ct)
///     {
///         var profile = new UserProfile(evt.UserId, evt.Name, evt.Email);
///         await _repository.UpsertAsync(profile, ct);
///     }
///     
///     public async ValueTask ProjectAsync(UserUpdated evt, CancellationToken ct)
///     {
///         var profile = await _repository.GetByIdAsync(evt.UserId, ct);
///         if (profile != null)
///         {
///             profile.Name = evt.Name;
///             profile.Email = evt.Email;
///             await _repository.UpsertAsync(profile, ct);
///         }
///     }
/// }
/// </code>
/// 
/// Registration with source generators:
/// <code>
/// services.AddInMemoryEventBus();
/// services.AddProjectionsFromAssembly(typeof(Program).Assembly);
/// </code>
/// </remarks>
public interface IProjectionHandler<in TEvent>
{
    /// <summary>
    /// Projects the event into a read model or materialized view.
    /// </summary>
    /// <param name="event">The event to project</param>
    /// <param name="ct">Cancellation token for async operations</param>
    /// <returns>A task that completes when the projection has been applied</returns>
    /// <remarks>
    /// This method is called by the projection engine when an event of type TEvent
    /// is processed from the event stream. Multiple handlers for the same event type
    /// are supported and will all be invoked.
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
