using BbQ.Outcome;
using Microsoft.Extensions.Logging;

namespace BbQ.Cqrs;

/// <summary>
/// A built-in pipeline behavior that logs request processing.
/// 
/// This behavior logs when a request starts being processed and when it completes,
/// along with the response value. Useful for debugging and monitoring request flow.
/// </summary>
/// <typeparam name="TRequest">The request type being processed</typeparam>
/// <typeparam name="TResponse">The response type returned by the handler</typeparam>
/// <remarks>
/// This behavior is automatically registered by AddBbQMediator() and is included
/// in the default pipeline. It executes as an outermost behavior to capture
/// the full request/response cycle.
/// 
/// Example output:
/// <code>
/// Handling CreateUserCommand
/// Handled CreateUserCommand -> Success: User { Id=..., Email=... }
/// </code>
/// </remarks>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> log)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _log = log;

    /// <summary>
    /// Logs the request type, executes the pipeline, and logs the response.
    /// </summary>
    public async Task<TResponse> Handle(
        TRequest request,
        CancellationToken ct,
        Func<TRequest, CancellationToken, Task<TResponse>> next)
    {
        _log.LogInformation("Handling {Request}", typeof(TRequest).Name);
        var response = await next(request, ct);
        _log.LogInformation("Handled {Request} -> {Response}", typeof(TRequest).Name, response?.ToString());
        return response;
    }
}
