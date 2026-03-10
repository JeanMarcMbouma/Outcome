using BbQ.Cqrs.DependencyInjection;
using BbQ.Outcome;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Tests to verify that CQRS dispatchers and mediator correctly resolve
/// scoped handlers and their scoped dependencies. This validates the fix
/// for the "Cannot resolve scoped service from root provider" issue that
/// occurs when dispatchers are singletons in ASP.NET Core applications.
/// </summary>
[TestFixture]
public class ScopedServiceResolutionTests
{
    [Test]
    public async Task CommandDispatcher_WithScopedHandler_ResolvesFromScope()
    {
        // Arrange - use ValidateScopes to mimic ASP.NET Core behavior
        var services = new ServiceCollection();
        services.AddBbQMediator([typeof(ScopedCommand).Assembly]);
        services.AddScoped<IScopedDependency, ScopedDependency>();
        services.AddScoped<IRequestHandler<ScopedCommand, Outcome<string>>, ScopedCommandHandler>();

        using var sp = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });

        // Act & Assert - should not throw "Cannot resolve scoped service from root provider"
        using var scope = sp.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
        var result = await dispatcher.Dispatch(new ScopedCommand("test"));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo("Scoped: test"));
    }

    [Test]
    public async Task QueryDispatcher_WithScopedHandler_ResolvesFromScope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBbQMediator([typeof(ScopedQuery).Assembly]);
        services.AddScoped<IScopedDependency, ScopedDependency>();
        services.AddScoped<IRequestHandler<ScopedQuery, Outcome<string>>, ScopedQueryHandler>();

        using var sp = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });

        // Act & Assert
        using var scope = sp.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();
        var result = await dispatcher.Dispatch(new ScopedQuery("test"));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo("Scoped: test"));
    }

    [Test]
    public async Task Mediator_WithScopedHandler_ResolvesFromScope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBbQMediator([typeof(ScopedCommand).Assembly]);
        services.AddScoped<IScopedDependency, ScopedDependency>();
        services.AddScoped<IRequestHandler<ScopedCommand, Outcome<string>>, ScopedCommandHandler>();

        using var sp = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });

        // Act & Assert
        using var scope = sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var result = await mediator.Send(new ScopedCommand("test"));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo("Scoped: test"));
    }

    [Test]
    public async Task CommandDispatcher_WithScopedBehavior_ResolvesFromScope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBbQMediator([typeof(ScopedCommand).Assembly]);
        services.AddScoped<IScopedDependency, ScopedDependency>();
        services.AddScoped<IRequestHandler<ScopedCommand, Outcome<string>>, ScopedCommandHandler>();
        services.AddScoped<IPipelineBehavior<ScopedCommand, Outcome<string>>, ScopedBehavior>();

        using var sp = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });

        // Act & Assert
        using var scope = sp.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
        var result = await dispatcher.Dispatch(new ScopedCommand("test"));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Does.Contain("[ScopedBehavior]"));
        Assert.That(result.Value, Does.Contain("Scoped: test"));
    }
}

// Scoped dependency to simulate real-world services like DbContext
public interface IScopedDependency
{
    string GetValue(string input);
}

public class ScopedDependency : IScopedDependency
{
    public string GetValue(string input) => $"Scoped: {input}";
}

// Command handler that depends on a scoped service
public record ScopedCommand(string Value) : ICommand<Outcome<string>>;

public class ScopedCommandHandler(IScopedDependency dependency)
    : IRequestHandler<ScopedCommand, Outcome<string>>
{
    public Task<Outcome<string>> Handle(ScopedCommand request, CancellationToken ct)
    {
        return Task.FromResult(Outcome<string>.From(dependency.GetValue(request.Value)));
    }
}

// Query handler that depends on a scoped service
public record ScopedQuery(string Value) : IQuery<Outcome<string>>;

public class ScopedQueryHandler(IScopedDependency dependency)
    : IRequestHandler<ScopedQuery, Outcome<string>>
{
    public Task<Outcome<string>> Handle(ScopedQuery request, CancellationToken ct)
    {
        return Task.FromResult(Outcome<string>.From(dependency.GetValue(request.Value)));
    }
}

// Scoped behavior
public class ScopedBehavior(IScopedDependency dependency)
    : IPipelineBehavior<ScopedCommand, Outcome<string>>
{
    public async Task<Outcome<string>> Handle(
        ScopedCommand request,
        CancellationToken ct,
        Func<ScopedCommand, CancellationToken, Task<Outcome<string>>> next)
    {
        // Use the scoped dependency to verify it was resolved correctly
        _ = dependency.GetValue("verify");
        var result = await next(request, ct);
        return result.IsSuccess
            ? Outcome<string>.From($"[ScopedBehavior] {result.Value}")
            : result;
    }
}
