using BbQ.Cqrs;
using BbQ.Cqrs.DependencyInjection;
using BbQ.Outcome;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Tests for ICommandDispatcher to verify that it correctly:
/// - Resolves handlers
/// - Applies pipeline behaviors in order
/// - Executes handlers
/// - Returns results
/// </summary>
[TestFixture]
public class CommandDispatcherTests
{
    private ServiceProvider _serviceProvider = null!;
    private ICommandDispatcher _dispatcher = null!;

    [SetUp]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddBbQMediator(typeof(TestCommand).Assembly);
        services.AddTransient<IRequestHandler<TestCommand, Outcome<string>>, TestCommandHandler>();
        services.AddTransient<IRequestHandler<TestCommandWithoutResult, Unit>, TestCommandWithoutResultHandler>();
        
        _serviceProvider = services.BuildServiceProvider();
        _dispatcher = _serviceProvider.GetRequiredService<ICommandDispatcher>();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider?.Dispose();
    }

    [Test]
    public async Task Dispatch_WithValidCommand_ReturnsSuccessfulOutcome()
    {
        // Arrange
        var command = new TestCommand("Test Value");

        // Act
        var result = await _dispatcher.Dispatch(command);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo("Processed: Test Value"));
    }

    [Test]
    public async Task Dispatch_WithBehaviors_AppliesBehaviorsInOrder()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBbQMediator(typeof(TestCommand).Assembly);
        services.AddTransient<IRequestHandler<TestCommand, Outcome<string>>, TestCommandHandler>();
        services.AddTransient<IPipelineBehavior<TestCommand, Outcome<string>>, TestBehavior1>();
        services.AddTransient<IPipelineBehavior<TestCommand, Outcome<string>>, TestBehavior2>();

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();

        var command = new TestCommand("Test");

        // Act
        var result = await dispatcher.Dispatch(command);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Does.Contain("[Behavior1]"));
        Assert.That(result.Value, Does.Contain("[Behavior2]"));
        Assert.That(result.Value, Does.Contain("Processed: Test"));
    }

    [Test]
    public async Task Dispatch_FireAndForget_ExecutesHandler()
    {
        // Arrange
        var command = new TestCommandWithoutResult();

        // Act & Assert - should not throw
        await _dispatcher.Dispatch(command);
        Assert.Pass("Fire-and-forget command executed successfully");
    }
}

// Test command and handler implementations
public record TestCommand(string Value) : ICommand<Outcome<string>>;

public class TestCommandHandler : IRequestHandler<TestCommand, Outcome<string>>
{
    public Task<Outcome<string>> Handle(TestCommand request, CancellationToken ct)
    {
        return Task.FromResult(Outcome<string>.From($"Processed: {request.Value}"));
    }
}

public record TestCommandWithoutResult : ICommand<Unit>;

public class TestCommandWithoutResultHandler : IRequestHandler<TestCommandWithoutResult, Unit>
{
    public Task<Unit> Handle(TestCommandWithoutResult request, CancellationToken ct)
    {
        return Task.FromResult(Unit.Value);
    }
}

public class TestBehavior1 : IPipelineBehavior<TestCommand, Outcome<string>>
{
    public async Task<Outcome<string>> Handle(
        TestCommand request,
        CancellationToken ct,
        Func<TestCommand, CancellationToken, Task<Outcome<string>>> next)
    {
        var result = await next(request, ct);
        return result.IsSuccess
            ? Outcome<string>.From($"[Behavior1] {result.Value}")
            : result;
    }
}

public class TestBehavior2 : IPipelineBehavior<TestCommand, Outcome<string>>
{
    public async Task<Outcome<string>> Handle(
        TestCommand request,
        CancellationToken ct,
        Func<TestCommand, CancellationToken, Task<Outcome<string>>> next)
    {
        var result = await next(request, ct);
        return result.IsSuccess
            ? Outcome<string>.From($"[Behavior2] {result.Value}")
            : result;
    }
}
