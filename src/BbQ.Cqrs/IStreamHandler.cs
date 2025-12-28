// -------------------------------
// Core contracts (Outcome-centric)
// -------------------------------
using System.Runtime.CompilerServices;

namespace BbQ.Cqrs;

/// <summary>
/// Handler contract for processing streaming requests in the CQRS pipeline.
/// 
/// Implementations handle a specific streaming request type and produce a stream
/// of items. The handler is the terminal point in the pipeline, after all behaviors
/// have executed.
/// </summary>
/// <typeparam name="TRequest">The streaming request type to handle, must implement IStreamRequest&lt;TItem&gt;</typeparam>
/// <typeparam name="TItem">The type of items produced by the stream</typeparam>
/// <remarks>
/// Implementation guidelines:
/// - Each streaming request type should have exactly one handler
/// - Handlers should be registered with the service container (AddBbQMediator)
/// - Handlers receive cancellation tokens to support graceful cancellation
/// - Use dependency injection to access repositories, services, validators, etc.
/// - Use [EnumeratorCancellation] attribute on the CancellationToken parameter
/// - Use 'yield return' to produce items in the stream
/// 
/// Example handler:
/// <code>
/// public class StreamUsersQueryHandler : IStreamHandler&lt;StreamUsersQuery, User&gt;
/// {
///     private readonly IUserRepository _repository;
///     private readonly ILogger&lt;StreamUsersQueryHandler&gt; _logger;
///     
///     public StreamUsersQueryHandler(
///         IUserRepository repository,
///         ILogger&lt;StreamUsersQueryHandler&gt; logger)
///     {
///         _repository = repository;
///         _logger = logger;
///     }
///     
///     public async IAsyncEnumerable&lt;User&gt; Handle(
///         StreamUsersQuery request, 
///         [EnumeratorCancellation] CancellationToken ct)
///     {
///         _logger.LogInformation("Starting to stream users");
///         
///         var offset = 0;
///         while (true)
///         {
///             var batch = await _repository.GetBatchAsync(offset, request.PageSize, ct);
///             if (batch.Count == 0) break;
///             
///             foreach (var user in batch)
///             {
///                 ct.ThrowIfCancellationRequested();
///                 yield return user;
///             }
///             
///             offset += batch.Count;
///         }
///         
///         _logger.LogInformation("Finished streaming users");
///     }
/// }
/// </code>
/// </remarks>
public interface IStreamHandler<TRequest, TItem>
    where TRequest : IStreamRequest<TItem>
{
    /// <summary>
    /// Handles the streaming request and returns an asynchronous stream of items.
    /// </summary>
    /// <param name="request">The request to process</param>
    /// <param name="ct">Cancellation token for async operations. Use [EnumeratorCancellation] attribute.</param>
    /// <returns>An asynchronous stream of items</returns>
    /// <remarks>
    /// This method is called at the end of the pipeline after all behaviors
    /// have been executed. It should contain the core business logic for
    /// producing the stream of items.
    /// 
    /// The method should:
    /// - Use 'yield return' to produce items incrementally
    /// - Check cancellation token periodically
    /// - Handle errors appropriately (exceptions will terminate the stream)
    /// - Use [EnumeratorCancellation] on the CancellationToken parameter
    /// </remarks>
    IAsyncEnumerable<TItem> Handle(TRequest request, [EnumeratorCancellation] CancellationToken ct);
}
