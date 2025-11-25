// -------------------------------
// Core contracts (Outcome-centric)
// -------------------------------
namespace BbQ.Cqrs;

/// <summary>
/// Marker interface for queries in the CQRS pattern.
/// 
/// Queries represent read-only operations that do not modify state.
/// They can be executed multiple times with the same input and always
/// return the same result (idempotent).
/// </summary>
/// <typeparam name="TResponse">The type of data returned by this query</typeparam>
/// <remarks>
/// Usage pattern:
/// <code>
/// // Define the query
/// public class GetUserByIdQuery : IQuery&lt;User&gt;
/// {
///     public Guid UserId { get; set; }
/// }
/// 
/// // Implement the handler
/// public class GetUserByIdQueryHandler : IRequestHandler&lt;GetUserByIdQuery, User&gt;
/// {
///     private readonly IUserRepository _repository;
///     
///     public async Task&lt;User&gt; Handle(GetUserByIdQuery request, CancellationToken ct)
///     {
///         return await _repository.GetByIdAsync(request.UserId, ct);
///     }
/// }
/// 
/// // Send the query
/// var user = await mediator.Send(new GetUserByIdQuery { UserId = userId });
/// </code>
/// 
/// Best practices:
/// - Use IQuery for read-only operations (no side effects)
/// - Be idempotent - same input always returns same output
/// - Consider caching behavior
/// - Use Outcome&lt;T&gt; if the query might fail (e.g., resource not found)
/// - Follow naming convention ending with "Query"
/// </remarks>
public interface IQuery<TResponse> : IRequest<TResponse> { }
