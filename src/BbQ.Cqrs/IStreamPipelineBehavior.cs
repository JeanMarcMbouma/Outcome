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
/// Pipeline execution order (FIFO before handler, LIFO after handler):
/// 1. First registered behavior executes first (outermost)
/// 2. Each behavior can execute logic before calling next()
/// 3. Behaviors are nested, so they execute in registration order going toward the handler
/// 4. At the end of the chain, the handler is invoked
/// 5. Each behavior can transform, filter, or augment the stream from next()
/// 6. Behaviors return in reverse order, so first registered returns last
/// 
/// Example with Behavior1 registered first, Behavior2 registered second:
/// <code>
/// Behavior1 (before) → Behavior2 (before) → Handler → Behavior2 (stream processing) → Behavior1 (stream processing)
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
