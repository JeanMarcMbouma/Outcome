// ---------------------------
// Test utilities for extension
// ---------------------------
namespace BbQ.Cqrs.Testing;

/// <summary>
/// A simple stub handler implementation for unit testing.
/// 
/// Use this to create test handlers without implementing the interface manually.
/// Pass a lambda function to define the handler's behavior.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
/// <remarks>
/// Example usage in tests:
/// <code>
/// // Create a stub that returns a successful outcome
/// var handler = new StubHandler&lt;GetUserByIdQuery, User&gt;(
///     async (request, ct) => 
///     {
///         return new User { Id = request.UserId, Email = "test@example.com" };
///     }
/// );
/// 
/// // Create a stub that returns an error
/// var failingHandler = new StubHandler&lt;GetUserByIdQuery, Outcome&lt;User&gt;&gt;(
///     async (request, ct) => 
///     {
///         return UserErrorCodeErrors.NotFoundError.ToOutcome&lt;User&gt;();
///     }
/// );
/// 
/// // Use with TestMediator
/// var mediator = new TestMediator&lt;GetUserByIdQuery, User&gt;(handler, behaviors);
/// var result = await mediator.Send(new GetUserByIdQuery { UserId = userId });
/// </code>
/// </remarks>
public sealed class StubHandler<TRequest, TResponse>
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly Func<TRequest, CancellationToken, Task<TResponse>> _impl;

    /// <summary>
    /// Creates a stub handler with the provided implementation function.
    /// </summary>
    /// <param name="impl">
    /// A function that takes a request and cancellation token, and returns a response.
    /// This function defines the handler's behavior for testing.
    /// </param>
    public StubHandler(Func<TRequest, CancellationToken, Task<TResponse>> impl) => _impl = impl;

    /// <summary>
    /// Handles the request by delegating to the implementation function.
    /// </summary>
    public Task<TResponse> Handle(TRequest request, CancellationToken ct) => _impl(request, ct);
}
