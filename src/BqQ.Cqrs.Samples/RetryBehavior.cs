using BbQ.Cqrs;
using BbQ.Outcome;

namespace BqQ.CQRS.Samples;

// A retry behavior that only wraps commands and respects transient errors
public sealed class RetryBehavior<TRequest, TResponse, TPayload>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IOutcome<TPayload>
{
    private readonly int _maxAttempts;
    private readonly TimeSpan _delay;

    public RetryBehavior(int maxAttempts = 3, TimeSpan? delay = null)
    {
        _maxAttempts = Math.Max(1, maxAttempts);
        _delay = delay ?? TimeSpan.FromMilliseconds(150);
    }

    public async Task<TResponse> Handle(
        TRequest request,
        CancellationToken ct,
        Func<TRequest, CancellationToken, Task<TResponse>> next)
    {
        for (var attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            var outcome = await next(request, ct);

            var (ok, value, errors) = outcome;
            if (ok) return outcome;

            // If any error is transient, retry; otherwise, stop immediately
            var retryable = errors?.OfType<Error<AppError>>().Any(e => e.Code == AppError.Transient) ?? false;
            if (!retryable || attempt == _maxAttempts) return outcome;

            await Task.Delay(_delay, ct);
        }

        // Unreachable due to return inside loop; added for completeness.
        return await next(request, ct);
    }
}
