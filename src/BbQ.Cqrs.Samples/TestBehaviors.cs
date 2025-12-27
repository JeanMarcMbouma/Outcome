using BbQ.Cqrs;
using BbQ.Outcome;

namespace BbQ.CQRS.Samples;

// This behavior should NOT trigger the analyzer because it has exactly 2 type parameters
[Behavior(Order = 10)]
public class TestValidBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest request, CancellationToken ct, Func<TRequest, CancellationToken, Task<TResponse>> next)
    {
        return next(request, ct);
    }
}
