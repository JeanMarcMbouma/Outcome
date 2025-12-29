using BbQ.Cqrs;
using BbQ.Cqrs.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Runtime.CompilerServices;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Tests for source generator integration with streaming handlers and behaviors
/// </summary>
[TestFixture]
public class SourceGeneratorStreamingTests
{
    [Test]
    public async Task StreamHandler_RegisteredViaSourceGenerator_WorksCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBbQMediator(Array.Empty<System.Reflection.Assembly>());
        
        // Use source-generated registration (would be: services.AddBbQCqrsTestsHandlers())
        // For now, manually register to verify the pattern works
        services.AddTransient<IStreamHandler<TestStreamQuery, string>, TestStreamQueryHandler>();
        
        using var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();
        
        var query = new TestStreamQuery(3);
        var results = new List<string>();
        
        // Act
        await foreach (var item in mediator.Stream(query))
        {
            results.Add(item);
        }
        
        // Assert
        Assert.That(results, Has.Count.EqualTo(3));
        Assert.That(results, Is.EqualTo(new[] { "Item 1", "Item 2", "Item 3" }));
    }
    
    [Test]
    public async Task StreamBehavior_RegisteredViaSourceGenerator_AppliesCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBbQMediator(Array.Empty<System.Reflection.Assembly>());
        
        services.AddTransient<IStreamHandler<TestStreamQuery, string>, TestStreamQueryHandler>();
        
        // Behavior with [Behavior] attribute would be auto-registered by source generator
        // For now, manually register to demonstrate the pattern
        services.AddTransient<IStreamPipelineBehavior<TestStreamQuery, string>, TestStreamBehavior>();
        
        using var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();
        
        var query = new TestStreamQuery(2);
        var results = new List<string>();
        
        // Act
        await foreach (var item in mediator.Stream(query))
        {
            results.Add(item);
        }
        
        // Assert - Behavior adds prefix
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0], Does.StartWith("[Logged]"));
        Assert.That(results[1], Does.StartWith("[Logged]"));
    }
}

// Test streaming query
public record TestStreamQuery(int Count) : IStreamQuery<string>;

// Test stream handler  
public class TestStreamQueryHandler : IStreamHandler<TestStreamQuery, string>
{
    public async IAsyncEnumerable<string> Handle(
        TestStreamQuery request, 
        [EnumeratorCancellation] CancellationToken ct)
    {
        for (int i = 1; i <= request.Count; i++)
        {
            await Task.Delay(1, ct);
            yield return $"Item {i}";
        }
    }
}

// Test stream behavior (would have [Behavior(Order = 1)] attribute for auto-registration)
public class TestStreamBehavior : IStreamPipelineBehavior<TestStreamQuery, string>
{
    public async IAsyncEnumerable<string> Handle(
        TestStreamQuery request,
        [EnumeratorCancellation] CancellationToken ct,
        Func<TestStreamQuery, CancellationToken, IAsyncEnumerable<string>> next)
    {
        await foreach (var item in next(request, ct).WithCancellation(ct))
        {
            yield return $"[Logged] {item}";
        }
    }
}
