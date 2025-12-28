// -------------------------------
// Core contracts (Outcome-centric)
// -------------------------------
namespace BbQ.Cqrs;

/// <summary>
/// The core mediator interface for the CQRS pattern.
/// 
/// Provides a single, strongly-typed entry point for sending commands and queries.
/// All request/response pairs are resolved at compile-time with full type safety.
/// </summary>
/// <remarks>
/// The mediator is responsible for:
/// - Resolving the appropriate handler for a request
/// - Building and executing the pipeline of behaviors
/// - Passing the request through all behaviors in order
/// - Invoking the handler at the terminal of the pipeline
/// 
/// No reflection is used at runtime; all types are bound at compile-time.
/// </remarks>
public interface IMediator
{
    /// <summary>
    /// Sends a request through the pipeline and returns a response.
    /// </summary>
    /// <typeparam name="TRequest">The request type, must implement IRequest&lt;TResponse&gt;</typeparam>
    /// <typeparam name="TResponse">The response type returned by the handler</typeparam>
    /// <param name="request">The request to send</param>
    /// <param name="ct">Optional cancellation token for async operations</param>
    /// <returns>A task containing the response from the handler</returns>
    /// <remarks>
    /// The request is passed through all registered IPipelineBehavior implementations
    /// in registration order, with the handler invoked at the end of the chain.
    /// </remarks>
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default);

    /// <summary>
    /// Sends a fire-and-forget request through the pipeline.
    /// </summary>
    /// <remarks>
    /// The request must implement IRequest (fire-and-forget variant).
    /// The handler must implement IRequestHandler&lt;TRequest&gt; (single type parameter).
    /// 
    /// The request is passed through all registered IPipelineBehavior&lt;TRequest, Unit&gt; 
    /// implementations in registration order, with the handler invoked at the end of the chain.
    /// 
    /// Unlike the generic Send method, this overload does not return a response value.
    /// It is useful for operations like sending emails, publishing events, or executing 
    /// commands where the return value is not important.
    /// </remarks>
    Task Send(IRequest request, CancellationToken ct = default);

    /// <summary>
    /// Sends a streaming request through the pipeline and returns a stream of items.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the stream</typeparam>
    /// <param name="request">The streaming request to send</param>
    /// <param name="ct">Optional cancellation token for async operations</param>
    /// <returns>An asynchronous stream of items from the handler</returns>
    /// <remarks>
    /// The request is passed through all registered IStreamPipelineBehavior implementations
    /// in registration order, with the handler invoked at the end of the chain.
    /// 
    /// Example usage:
    /// <code>
    /// await foreach (var user in mediator.Stream(new StreamAllUsersQuery(), ct))
    /// {
    ///     Console.WriteLine($"User: {user.Name}");
    /// }
    /// </code>
    /// </remarks>
    IAsyncEnumerable<TItem> Stream<TItem>(IStreamRequest<TItem> request, CancellationToken ct = default);
}
