// ---------------------------
// Test utilities for unit testing CQRS handlers and behaviors
// ---------------------------
namespace BbQ.Cqrs.Testing;

/// <summary>
/// A minimal mediator implementation for unit testing.
/// 
/// This test mediator allows you to test a specific handler and behavior combination
/// in isolation without needing a full dependency injection container.
/// </summary>
/// <typeparam name="TRequest">The request type being tested</typeparam>
/// <typeparam name="TResponse">The response type expected from the handler</typeparam>
/// <remarks>
/// Use TestMediator to:
/// - Test a single handler with specific behaviors
/// - Control the behavior pipeline for behavior isolation
/// - Avoid the complexity of setting up full DI containers
/// - Mock or stub handler implementations
/// 
/// Example unit test:
/// <code>
/// [Test]
/// public async Task CreateUserHandler_WithValidRequest_CreatesUser()
/// {
///     // Arrange
///     var mockRepository = new Mock&lt;IUserRepository&gt;();
///     var handler = new CreateUserCommandHandler(mockRepository.Object);
///     
///     var logging = new LoggingBehavior&lt;CreateUserCommand, Outcome&lt;User&gt;&gt;(mockLogger);
///     var validation = new ValidationBehavior&lt;CreateUserCommand, Outcome&lt;User&gt;&gt;(validator);
///     
///     var mediator = new TestMediator&lt;CreateUserCommand, Outcome&lt;User&gt;&gt;(
///         handler,
///         new[] { logging, validation }
///     );
///     
///     var command = new CreateUserCommand { Email = "test@example.com", Name = "Test" };
///     
///     // Act
///     var result = await mediator.Send(command);
///     
///     // Assert
///     Assert.That(result.IsSuccess, Is.True);
///     mockRepository.Verify(r => r.AddAsync(It.IsAny&lt;User&gt;(), It.IsAny&lt;CancellationToken&gt;()), Times.Once());
/// }
/// </code>
/// </remarks>
public sealed class TestMediator<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IRequestHandler<TRequest, TResponse> _handler;
    private readonly IEnumerable<IPipelineBehavior<TRequest, TResponse>> _behaviors;

    /// <summary>
    /// Creates a test mediator with a specific handler and optional behaviors.
    /// </summary>
    /// <param name="handler">The handler to test (usually a mock or stub)</param>
    /// <param name="behaviors">Optional pipeline behaviors to test alongside the handler</param>
    /// <remarks>
    /// The mediator will execute behaviors in order, with the handler as the terminal.
    /// If behaviors is null or empty, requests go directly to the handler.
    /// </remarks>
    public TestMediator(
        IRequestHandler<TRequest, TResponse> handler,
        IEnumerable<IPipelineBehavior<TRequest, TResponse>> behaviors)
    {
        _handler = handler;
        _behaviors = behaviors ?? [];
    }

    /// <summary>
    /// Sends a request through the test pipeline.
    /// </summary>
    /// <param name="request">The request to process</param>
    /// <param name="ct">Optional cancellation token</param>
    /// <returns>The response from the handler</returns>
    /// <remarks>
    /// This method mirrors the behavior of the production Mediator,
    /// composing behaviors in reverse order and invoking the handler at the terminal.
    /// </remarks>
    public Task<TResponse> Send(TRequest request, CancellationToken ct = default)
    {
        // Start with handler as terminal
        Func<TRequest, CancellationToken, Task<TResponse>> terminal =
            (req, token) => _handler.Handle(req, token);

        // Compose behaviors in reverse order
        foreach (var behavior in _behaviors.Reverse())
        {
            var next = terminal;
            terminal = (req, token) => behavior.Handle(req, token, next);
        }

        // Execute the pipeline
        return terminal(request, ct);
    }
}
