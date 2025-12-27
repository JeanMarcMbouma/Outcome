using BbQ.Cqrs;
using BbQ.Cqrs.DependencyInjection;
using BbQ.Outcome;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Tests for IMediator to verify backward compatibility with direct IRequest implementations.
/// This ensures that requests implementing IRequest directly (without ICommand or IQuery)
/// still work correctly.
/// </summary>
[TestFixture]
public class MediatorBackwardCompatibilityTests
{
    private ServiceProvider _serviceProvider = null!;
    private IMediator _mediator = null!;

    [SetUp]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddBbQMediator(typeof(DirectRequest).Assembly);
        services.AddTransient<IRequestHandler<DirectRequest, string>, DirectRequestHandler>();
        
        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider?.Dispose();
    }

    [Test]
    public async Task Send_WithDirectIRequestImplementation_WorksCorrectly()
    {
        // Arrange
        var request = new DirectRequest("test value");

        // Act
        var result = await _mediator.Send(request);

        // Assert
        Assert.That(result, Is.EqualTo("Handled: test value"));
    }

    [Test]
    public async Task Send_WithDirectIRequestImplementationAndBehaviors_AppliesBehaviors()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBbQMediator(typeof(DirectRequest).Assembly);
        services.AddTransient<IRequestHandler<DirectRequest, string>, DirectRequestHandler>();
        services.AddTransient<IPipelineBehavior<DirectRequest, string>, DirectRequestBehavior>();

        using var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var request = new DirectRequest("test");

        // Act
        var result = await mediator.Send(request);

        // Assert
        Assert.That(result, Does.Contain("[Behavior]"));
        Assert.That(result, Does.Contain("Handled: test"));
    }
}

// Test request that implements IRequest<TResponse> directly (not ICommand or IQuery)
// This is not recommended but should be supported for backward compatibility
public record DirectRequest(string Value) : IRequest<string>;

public class DirectRequestHandler : IRequestHandler<DirectRequest, string>
{
    public Task<string> Handle(DirectRequest request, CancellationToken ct)
    {
        return Task.FromResult($"Handled: {request.Value}");
    }
}

public class DirectRequestBehavior : IPipelineBehavior<DirectRequest, string>
{
    public async Task<string> Handle(
        DirectRequest request,
        CancellationToken ct,
        Func<DirectRequest, CancellationToken, Task<string>> next)
    {
        var result = await next(request, ct);
        return $"[Behavior] {result}";
    }
}
