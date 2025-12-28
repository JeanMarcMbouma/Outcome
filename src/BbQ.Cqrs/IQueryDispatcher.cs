// -------------------------------
// Core contracts (Outcome-centric)
// -------------------------------
namespace BbQ.Cqrs;

/// <summary>
/// Dispatcher interface for queries in the CQRS pattern.
/// 
/// The query dispatcher is responsible for:
/// - Taking a query request
/// - Resolving the correct handler
/// - Applying pipeline behaviors in order
/// - Executing the handler
/// - Returning the result
/// 
/// This interface defines a thin orchestrator over the query pipeline.
/// </summary>
/// <remarks>
/// Queries represent read-only operations that do not modify state.
/// The dispatcher ensures that all registered behaviors are applied before
/// the query reaches its handler.
/// 
/// Example usage:
/// <code>
/// public class UserController
/// {
///     private readonly IQueryDispatcher _queryDispatcher;
///     
///     public async Task&lt;IActionResult&gt; GetUser(GetUserByIdQuery query, CancellationToken ct)
///     {
///         var result = await _queryDispatcher.Dispatch(query, ct);
///         return result.Match(
///             onSuccess: user => Ok(user),
///             onError: errors => NotFound(errors)
///         );
///     }
/// }
/// </code>
/// </remarks>
public interface IQueryDispatcher
{
    /// <summary>
    /// Dispatches a query through the pipeline and returns a response.
    /// </summary>
    /// <typeparam name="TResponse">The response type returned by the query handler</typeparam>
    /// <param name="query">The query to dispatch</param>
    /// <param name="ct">Optional cancellation token for async operations</param>
    /// <returns>A task containing the response from the query handler</returns>
    /// <remarks>
    /// The query is passed through all registered IPipelineBehavior implementations
    /// in registration order. First registered behavior becomes outermost, creating:
    /// - FIFO (First In, First Out) order before handler execution
    /// - LIFO (Last In, First Out) order after handler execution
    /// 
    /// This means behaviors wrap like nested function calls:
    /// Behavior1 → Behavior2 → Handler → Behavior2 → Behavior1
    /// 
    /// Process:
    /// 1. Resolves the handler for the query type
    /// 2. Builds the pipeline with all registered behaviors
    /// 3. Executes behaviors in registration order (first registered executes first)
    /// 4. Invokes the handler
    /// 5. Returns through behaviors in reverse order (first registered returns last)
    /// </remarks>
    Task<TResponse> Dispatch<TResponse>(IQuery<TResponse> query, CancellationToken ct = default);

    /// <summary>
    /// Dispatches a streaming query through the pipeline and returns a stream of items.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the stream</typeparam>
    /// <param name="query">The streaming query to dispatch</param>
    /// <param name="ct">Optional cancellation token for async operations</param>
    /// <returns>An asynchronous stream of items from the query handler</returns>
    /// <remarks>
    /// The streaming query is passed through all registered IStreamPipelineBehavior implementations
    /// in registration order, with the handler invoked at the end of the chain.
    /// 
    /// Example usage:
    /// <code>
    /// await foreach (var user in queryDispatcher.Stream(new StreamAllUsersQuery(), ct))
    /// {
    ///     Console.WriteLine($"User: {user.Name}");
    /// }
    /// </code>
    /// </remarks>
    IAsyncEnumerable<TItem> Stream<TItem>(IStreamQuery<TItem> query, CancellationToken ct = default);
}
