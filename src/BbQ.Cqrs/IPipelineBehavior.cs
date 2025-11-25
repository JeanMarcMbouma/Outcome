// -------------------------------
// Core contracts (Outcome-centric)
// -------------------------------
namespace BbQ.Cqrs;

/// <summary>
/// Pipeline behavior contract for cross-cutting concerns in the CQRS pipeline.
/// 
/// Behaviors form a chain of responsibility that wraps the handler.
/// They execute before the handler is invoked and can modify the request,
/// intercept the response, or perform side effects like logging, validation, or caching.
/// </summary>
/// <typeparam name="TRequest">The request type, must implement IRequest&lt;TResponse&gt;</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
/// <remarks>
/// Pipeline execution order:
/// 1. First registered behavior executes first
/// 2. Each behavior can execute logic before calling next()
/// 3. At the end of the chain, the handler is invoked
/// 4. Each behavior can execute logic after the handler returns
/// 5. The outermost behavior's result is returned to the caller
/// 
/// Example:
/// <code>
/// // Logging behavior
/// public class LoggingBehavior&lt;TRequest, TResponse&gt; : IPipelineBehavior&lt;TRequest, TResponse&gt;
///     where TRequest : IRequest&lt;TResponse&gt;
/// {
///     private readonly ILogger&lt;LoggingBehavior&lt;TRequest, TResponse&gt;&gt; _logger;
///     
///     public async Task&lt;TResponse&gt; Handle(
///         TRequest request,
///         CancellationToken ct,
///         Func&lt;TRequest, CancellationToken, Task&lt;TResponse&gt;&gt; next)
///     {
///         _logger.LogInformation("Handling {RequestType}", typeof(TRequest).Name);
///         
///         var stopwatch = Stopwatch.StartNew();
///         var response = await next(request, ct);
///         stopwatch.Stop();
///         
///         _logger.LogInformation("Completed {RequestType} in {Elapsed}ms", 
///             typeof(TRequest).Name, stopwatch.ElapsedMilliseconds);
///         
///         return response;
///     }
/// }
/// 
/// // Validation behavior
/// public class ValidationBehavior&lt;TRequest, TResponse&gt; : IPipelineBehavior&lt;TRequest, TResponse&gt;
///     where TRequest : IRequest&lt;TResponse&gt;
/// {
///     private readonly IValidator&lt;TRequest&gt; _validator;
///     
///     public async Task&lt;TResponse&gt; Handle(
///         TRequest request,
///         CancellationToken ct,
///         Func&lt;TRequest, CancellationToken, Task&lt;TResponse&gt;&gt; next)
///     {
///         var validationResult = await _validator.ValidateAsync(request, ct);
///         if (!validationResult.IsValid)
///         {
///             // Return validation error (assumes TResponse can represent errors)
///             throw new ValidationException(validationResult.Errors);
///         }
///         
///         return await next(request, ct);
///     }
/// }
/// </code>
/// 
/// Common use cases:
/// - Logging request/response details and execution time
/// - Validating requests before they reach the handler
/// - Handling exceptions and converting them to domain errors
/// - Caching responses for queries
/// - Authorization and authentication checks
/// - Performance monitoring and metrics
/// - Transaction management
/// </remarks>
public interface IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Executes the behavior in the pipeline.
    /// </summary>
    /// <param name="request">The request being processed</param>
    /// <param name="ct">Cancellation token for async operations</param>
    /// <param name="next">
    /// A delegate to the next behavior in the pipeline or the handler.
    /// Must be called to continue the pipeline chain.
    /// </param>
    /// <returns>The response from the pipeline</returns>
    /// <remarks>
    /// Call the next() delegate to proceed to the next behavior or handler.
    /// You can execute code before and after calling next() to implement
    /// cross-cutting concerns.
    /// 
    /// If you need to short-circuit the pipeline (e.g., validation failure),
    /// return a response without calling next().
    /// </remarks>
    Task<TResponse> Handle(
        TRequest request,
        CancellationToken ct,
        Func<TRequest, CancellationToken, Task<TResponse>> next);
}
