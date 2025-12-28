// -------------------------------
// Core contracts (Outcome-centric)
// -------------------------------
namespace BbQ.Cqrs;

/// <summary>
/// Marker interface for streaming queries in the CQRS pattern.
/// 
/// Streaming queries represent read-only operations that return a stream of items
/// rather than a single result. They are useful for:
/// - Large result sets that should be processed incrementally
/// - Real-time data streams
/// - Event subscriptions
/// - Long-running queries with progressive results
/// </summary>
/// <typeparam name="TItem">The type of items in the stream</typeparam>
/// <remarks>
/// Usage pattern:
/// <code>
/// // Define the streaming query
/// public class StreamAllUsersQuery : IStreamQuery&lt;User&gt;
/// {
///     public int PageSize { get; set; } = 100;
/// }
/// 
/// // Implement the handler
/// public class StreamAllUsersQueryHandler : IStreamHandler&lt;StreamAllUsersQuery, User&gt;
/// {
///     private readonly IUserRepository _repository;
///     
///     public async IAsyncEnumerable&lt;User&gt; Handle(
///         StreamAllUsersQuery request, 
///         [EnumeratorCancellation] CancellationToken ct)
///     {
///         await foreach (var user in _repository.StreamAllAsync(request.PageSize, ct))
///         {
///             yield return user;
///         }
///     }
/// }
/// 
/// // Use the streaming query
/// await foreach (var user in mediator.Stream(new StreamAllUsersQuery()))
/// {
///     Console.WriteLine($"Processing user: {user.Name}");
/// }
/// </code>
/// 
/// Best practices:
/// - Use for read-only operations (no side effects)
/// - Consider memory implications of buffering
/// - Support cancellation via CancellationToken
/// - Use [EnumeratorCancellation] attribute on CancellationToken parameter
/// - Follow naming convention ending with "Query"
/// </remarks>
public interface IStreamQuery<TItem> : IStreamRequest<TItem> { }
