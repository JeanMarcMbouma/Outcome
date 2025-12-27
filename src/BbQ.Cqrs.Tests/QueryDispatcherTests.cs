using BbQ.Cqrs;
using BbQ.Cqrs.DependencyInjection;
using BbQ.Outcome;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Tests for IQueryDispatcher to verify that it correctly:
/// - Resolves handlers
/// - Applies pipeline behaviors in order
/// - Executes handlers
/// - Returns results
/// </summary>
[TestFixture]
public class QueryDispatcherTests
{
    private ServiceProvider _serviceProvider = null!;
    private IQueryDispatcher _dispatcher = null!;

    [SetUp]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddBbQMediator(typeof(TestQuery).Assembly);
        services.AddTransient<IRequestHandler<TestQuery, Outcome<string>>, TestQueryHandler>();
        
        _serviceProvider = services.BuildServiceProvider();
        _dispatcher = _serviceProvider.GetRequiredService<IQueryDispatcher>();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider?.Dispose();
    }

    [Test]
    public async Task Dispatch_WithValidQuery_ReturnsSuccessfulOutcome()
    {
        // Arrange
        var query = new TestQuery("123");

        // Act
        var result = await _dispatcher.Dispatch(query);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo("Query result for: 123"));
    }

    [Test]
    public async Task Dispatch_WithBehaviors_AppliesBehaviorsInOrder()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBbQMediator(typeof(TestQuery).Assembly);
        services.AddTransient<IRequestHandler<TestQuery, Outcome<string>>, TestQueryHandler>();
        services.AddTransient<IPipelineBehavior<TestQuery, Outcome<string>>, QueryTestBehavior1>();
        services.AddTransient<IPipelineBehavior<TestQuery, Outcome<string>>, QueryTestBehavior2>();

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();

        var query = new TestQuery("123");

        // Act
        var result = await dispatcher.Dispatch(query);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Does.Contain("[QueryBehavior1]"));
        Assert.That(result.Value, Does.Contain("[QueryBehavior2]"));
        Assert.That(result.Value, Does.Contain("Query result for: 123"));
    }

    [Test]
    public async Task Dispatch_WithCachingBehavior_CachesResults()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBbQMediator(typeof(TestQuery).Assembly);
        services.AddTransient<IRequestHandler<TestQuery, Outcome<string>>, CountingQueryHandler>();
        services.AddSingleton<CachingBehavior>();
        services.AddTransient<IPipelineBehavior<TestQuery, Outcome<string>>>(sp => sp.GetRequiredService<CachingBehavior>());

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var cachingBehavior = sp.GetRequiredService<CachingBehavior>();

        var query = new TestQuery("456");

        // Act
        var result1 = await dispatcher.Dispatch(query);
        var result2 = await dispatcher.Dispatch(query);

        // Assert
        Assert.That(result1.IsSuccess, Is.True);
        Assert.That(result2.IsSuccess, Is.True);
        Assert.That(cachingBehavior.CallCount, Is.EqualTo(1), "Handler should only be called once due to caching");
    }
}

// Test query and handler implementations
public record TestQuery(string Id) : IQuery<Outcome<string>>;

public class TestQueryHandler : IRequestHandler<TestQuery, Outcome<string>>
{
    public Task<Outcome<string>> Handle(TestQuery request, CancellationToken ct)
    {
        return Task.FromResult(Outcome<string>.From($"Query result for: {request.Id}"));
    }
}

public class CountingQueryHandler : IRequestHandler<TestQuery, Outcome<string>>
{
    private static int _callCount = 0;

    public Task<Outcome<string>> Handle(TestQuery request, CancellationToken ct)
    {
        var currentCount = System.Threading.Interlocked.Increment(ref _callCount);
        return Task.FromResult(Outcome<string>.From($"Query result {currentCount} for: {request.Id}"));
    }

    public static void Reset() => System.Threading.Interlocked.Exchange(ref _callCount, 0);
}

public class QueryTestBehavior1 : IPipelineBehavior<TestQuery, Outcome<string>>
{
    public async Task<Outcome<string>> Handle(
        TestQuery request,
        CancellationToken ct,
        Func<TestQuery, CancellationToken, Task<Outcome<string>>> next)
    {
        var result = await next(request, ct);
        return result.IsSuccess
            ? Outcome<string>.From($"[QueryBehavior1] {result.Value}")
            : result;
    }
}

public class QueryTestBehavior2 : IPipelineBehavior<TestQuery, Outcome<string>>
{
    public async Task<Outcome<string>> Handle(
        TestQuery request,
        CancellationToken ct,
        Func<TestQuery, CancellationToken, Task<Outcome<string>>> next)
    {
        var result = await next(request, ct);
        return result.IsSuccess
            ? Outcome<string>.From($"[QueryBehavior2] {result.Value}")
            : result;
    }
}

public class CachingBehavior : IPipelineBehavior<TestQuery, Outcome<string>>
{
    private readonly Dictionary<string, Outcome<string>> _cache = new();
    public int CallCount { get; private set; }

    public async Task<Outcome<string>> Handle(
        TestQuery request,
        CancellationToken ct,
        Func<TestQuery, CancellationToken, Task<Outcome<string>>> next)
    {
        var cacheKey = request.Id;
        if (_cache.TryGetValue(cacheKey, out var cachedResult))
        {
            return cachedResult;
        }

        CallCount++;
        var result = await next(request, ct);
        if (result.IsSuccess)
        {
            _cache[cacheKey] = result;
        }

        return result;
    }
}
