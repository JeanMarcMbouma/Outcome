// ---------------------------
// Test utilities for extension
// ---------------------------
namespace BbQ.Cqrs.Testing;

// Simple stub handler for tests
public sealed class StubHandler<TRequest, TResponse>
    : BbQ.Cqrs.IRequestHandler<TRequest, TResponse>
    where TRequest : BbQ.Cqrs.IRequest<TResponse>
{
    private readonly Func<TRequest, CancellationToken, Task<TResponse>> _impl;
    public StubHandler(Func<TRequest, CancellationToken, Task<TResponse>> impl) => _impl = impl;
    public Task<TResponse> Handle(TRequest request, CancellationToken ct) => _impl(request, ct);
}
