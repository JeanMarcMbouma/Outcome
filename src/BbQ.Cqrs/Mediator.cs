// -------------------------------
// Core contracts (Outcome-centric)
// -------------------------------
using BbQ.Cqrs;
using Microsoft.Extensions.DependencyInjection;

namespace BbQ.Cqrs;

internal sealed class Mediator : IMediator
{
    private readonly IServiceProvider _sp;

    public Mediator(IServiceProvider sp) => _sp = sp;

    public Task<TResponse> Send<TRequest, TResponse>(TRequest request, CancellationToken ct = default)
        where TRequest : IRequest<TResponse>
    {
        // Resolve strongly-typed handler
        var handler = _sp.GetRequiredService<IRequestHandler<TRequest, TResponse>>();

        // Resolve behaviors in registration order (outermost first)
        // Microsoft DI preserves registration order. We wrap in reverse to build the pipeline.
        var behaviors = _sp.GetServices<IPipelineBehavior<TRequest, TResponse>>();

        Func<TRequest, CancellationToken, Task<TResponse>> terminal =
            (req, token) => handler.Handle(req, token);

        foreach (var behavior in behaviors.Reverse())
        {
            var next = terminal;
            terminal = (req, token) => behavior.Handle(req, token, next);
        }

        return terminal(request, ct);
    }
}
