// ---------------------------
// Test utilities for extension
// ---------------------------
namespace BbQ.Cqrs.Testing;

// A minimal "unit-test mediator" for behavior isolation:
// - Inject a stub handler implementation
// - Compose an arbitrary set/order of behaviors
public sealed class TestMediator<TRequest, TResponse>
    where TRequest : BbQ.Cqrs.IRequest<TResponse>
{
    private readonly BbQ.Cqrs.IRequestHandler<TRequest, TResponse> _handler;
    private readonly IEnumerable<BbQ.Cqrs.IPipelineBehavior<TRequest, TResponse>> _behaviors;

    public TestMediator(
        BbQ.Cqrs.IRequestHandler<TRequest, TResponse> handler,
        IEnumerable<BbQ.Cqrs.IPipelineBehavior<TRequest, TResponse>> behaviors)
    {
        _handler = handler;
        _behaviors = behaviors ?? Enumerable.Empty<BbQ.Cqrs.IPipelineBehavior<TRequest, TResponse>>();
    }

    public Task<TResponse> Send(TRequest request, CancellationToken ct = default)
    {
        Func<TRequest, CancellationToken, Task<TResponse>> terminal =
            (req, token) => _handler.Handle(req, token);

        foreach (var behavior in _behaviors.Reverse())
        {
            var next = terminal;
            terminal = (req, token) => behavior.Handle(req, token, next);
        }

        return terminal(request, ct);
    }
}
