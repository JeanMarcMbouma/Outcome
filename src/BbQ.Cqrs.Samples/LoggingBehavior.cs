using BbQ.Cqrs;
using BbQ.Outcome;
using Microsoft.Extensions.Logging;

namespace BbQ.CQRS.Samples;

// -------------------
// Example behaviors
// -------------------
public sealed class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _log;
    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> log) => _log = log;

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
