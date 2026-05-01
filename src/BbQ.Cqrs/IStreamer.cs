// -------------------------------
// Core contracts (Outcome-centric)
// -------------------------------
namespace BbQ.Cqrs
{
    public interface IStreamer
    {
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
}