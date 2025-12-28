// -------------------------------
// Core contracts (Outcome-centric)
// -------------------------------
namespace BbQ.Cqrs;

/// <summary>
/// Base marker interface for streaming requests in the CQRS pipeline.
/// 
/// Represents a request that will be sent through the mediator to be handled 
/// by an IStreamHandler&lt;TRequest, TItem&gt; which returns a stream of items.
/// </summary>
/// <typeparam name="TItem">The type of items in the stream</typeparam>
/// <remarks>
/// Streaming requests enable real-time projections, event-stream subscriptions,
/// long-running queries, and other reactive workflows.
/// 
/// Example:
/// <code>
/// public class StreamUsersQuery : IStreamQuery&lt;User&gt; { }
/// </code>
/// 
/// The handler returns IAsyncEnumerable&lt;TItem&gt; which allows:
/// - Progressive/incremental data delivery
/// - Memory-efficient processing of large datasets
/// - Real-time event streaming
/// - Cancellation support via CancellationToken
/// </remarks>
public interface IStreamRequest<TItem> { }
