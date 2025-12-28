// -------------------------------
// Core contracts (Outcome-centric)
// -------------------------------
using System.Runtime.CompilerServices;

namespace BbQ.Cqrs;

/// <summary>
/// Pipeline behavior contract for cross-cutting concerns in streaming CQRS pipelines.
/// 
/// Streaming behaviors form a chain of responsibility that wraps the streaming handler.
/// They execute before the handler is invoked and can intercept, transform, or filter
/// the stream of items returned by the handler.
/// </summary>
/// <typeparam name="TRequest">The streaming request type, must implement IStreamRequest&lt;TItem&gt;</typeparam>
/// <typeparam name="TItem">The type of items in the stream</typeparam>
/// <remarks>
/// Execution model for streaming pipelines:
/// 1. Behaviors are registered outermost-first: the first registered behavior wraps all others.
/// 2. When a behavior calls next(), it receives an IAsyncEnumerable that has not yet been consumed.
/// 3. The handler (at the end of the chain) produces items into the stream.
/// 4. As items are yielded by the handler, they flow outward through behaviors in reverse registration order.
/// 5. Each behavior can observe, transform, filter, or augment items as they pass through.
/// 
/// Example with Behavior1 registered first (outermost) and Behavior2 registered second (inner):
/// <code>
/// Behavior1 wraps Behavior2 wraps Handler
/// Handler yields items → Behavior2 processes items → Behavior1 processes items
/// </code>
/// 
/// Example logging behavior:
/// <code>
/// public class StreamLoggingBehavior&lt;TRequest, TItem&gt; : IStreamPipelineBehavior&lt;TRequest, TItem&gt;
///     where TRequest : IStreamRequest&lt;TItem&gt;
/// {
///     private readonly ILogger _logger;
///     
///     public async IAsyncEnumerable&lt;TItem&gt; Handle(
///         TRequest request,
///         [EnumeratorCancellation] CancellationToken ct,
///         Func&lt;TRequest, CancellationToken, IAsyncEnumerable&lt;TItem&gt;&gt; next)
///     {
///         _logger.LogInformation("Starting stream for {RequestType}", typeof(TRequest).Name);
///         
///         var itemCount = 0;
///         await foreach (var item in next(request, ct).WithCancellation(ct))
///         {
///             itemCount++;
///             yield return item;
///         }
///         
///         _logger.LogInformation("Stream completed with {Count} items", itemCount);
///     }
/// }
/// </code>
/// 
/// Example filtering behavior:
/// <code>
/// public class StreamFilterBehavior&lt;TRequest, TItem&gt; : IStreamPipelineBehavior&lt;TRequest, TItem&gt;
///     where TRequest : IStreamRequest&lt;TItem&gt;, IFilterable&lt;TItem&gt;
/// {
///     public async IAsyncEnumerable&lt;TItem&gt; Handle(
///         TRequest request,
///         [EnumeratorCancellation] CancellationToken ct,
///         Func&lt;TRequest, CancellationToken, IAsyncEnumerable&lt;TItem&gt;&gt; next)
///     {
///         await foreach (var item in next(request, ct).WithCancellation(ct))
///         {
///             if (request.Filter(item))
///             {
///                 yield return item;
///             }
///         }
///     }
/// }
/// </code>
/// 
/// Common use cases:
/// - Logging stream start/completion and item counts
/// - Filtering items based on criteria
/// - Transforming items (though consider projection in query instead)
/// - Rate limiting or throttling
/// - Buffering or batching items
/// - Performance monitoring and metrics
/// - Error handling and retry logic
/// </remarks>
public interface IStreamPipelineBehavior<TRequest, TItem>
    where TRequest : IStreamRequest<TItem>
{
    /// <summary>
    /// Executes the streaming behavior in the pipeline.
    /// </summary>
    /// <param name="request">The streaming request being processed</param>
    /// <param name="ct">Cancellation token for async operations. Use [EnumeratorCancellation] attribute.</param>
    /// <param name="next">
    /// A delegate to the next behavior in the pipeline or the handler.
    /// Must be called to continue the pipeline chain.
    /// </param>
    /// <returns>An asynchronous stream of items</returns>
    /// <remarks>
    /// Call the next() delegate to proceed to the next behavior or handler.
    /// You can:
    /// - Execute code before calling next() (setup, validation)
    /// - Process each item from the stream using 'await foreach'
    /// - Filter, transform, or augment items in the stream
    /// - Count items, measure performance, or collect metrics
    /// - Handle errors from downstream behaviors/handler
    /// 
    /// Use [EnumeratorCancellation] on the CancellationToken parameter to
    /// ensure cancellation works correctly through the async enumerable.
    /// 
    /// Always use .WithCancellation(ct) when enumerating the stream from next().
    /// </remarks>
    IAsyncEnumerable<TItem> Handle(
        TRequest request,
        [EnumeratorCancellation] CancellationToken ct,
        Func<TRequest, CancellationToken, IAsyncEnumerable<TItem>> next);
}
