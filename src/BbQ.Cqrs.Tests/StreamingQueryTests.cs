using BbQ.Cqrs;
using BbQ.Cqrs.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Runtime.CompilerServices;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Tests for streaming query functionality to verify that it correctly:
/// - Resolves stream handlers
/// - Applies stream pipeline behaviors in order
/// - Executes stream handlers
/// - Returns streams of items
/// - Supports cancellation
/// </summary>
[TestFixture]
public class StreamingQueryTests
{
    [Test]
    public async Task Stream_WithValidQuery_ReturnsAllItems()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBbQMediator(typeof(StreamNumbersQuery).Assembly);
        services.AddTransient<IStreamHandler<StreamNumbersQuery, int>, StreamNumbersQueryHandler>();

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();

        var query = new StreamNumbersQuery(5);
        var results = new List<int>();

        // Act
        await foreach (var item in dispatcher.Stream(query))
        {
            results.Add(item);
        }

        // Assert
        Assert.That(results, Has.Count.EqualTo(5));
        Assert.That(results, Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
    }

    [Test]
    public async Task Stream_ThroughMediator_ReturnsAllItems()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBbQMediator(typeof(StreamNumbersQuery).Assembly);
        services.AddTransient<IStreamHandler<StreamNumbersQuery, int>, StreamNumbersQueryHandler>();

        using var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var query = new StreamNumbersQuery(3);
        var results = new List<int>();

        // Act
        await foreach (var item in mediator.Stream(query))
        {
            results.Add(item);
        }

        // Assert
        Assert.That(results, Has.Count.EqualTo(3));
        Assert.That(results, Is.EqualTo(new[] { 0, 1, 2 }));
    }

    [Test]
    public async Task Stream_WithBehavior_AppliesBehaviorToStream()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBbQMediator(typeof(StreamNumbersQuery).Assembly);
        services.AddTransient<IStreamHandler<StreamNumbersQuery, int>, StreamNumbersQueryHandler>();
        
        // Use a behavior instance to track count
        var loggingBehavior = new StreamLoggingBehavior();
        services.AddSingleton<IStreamPipelineBehavior<StreamNumbersQuery, int>>(loggingBehavior);

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();

        var query = new StreamNumbersQuery(4);
        var results = new List<int>();

        // Act
        await foreach (var item in dispatcher.Stream(query))
        {
            results.Add(item);
        }

        // Assert
        Assert.That(results, Has.Count.EqualTo(4));
        Assert.That(loggingBehavior.ItemCount, Is.EqualTo(4));
    }

    [Test]
    public async Task Stream_WithFilterBehavior_FiltersItems()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBbQMediator(typeof(StreamNumbersQuery).Assembly);
        services.AddTransient<IStreamHandler<StreamNumbersQuery, int>, StreamNumbersQueryHandler>();
        services.AddTransient<IStreamPipelineBehavior<StreamNumbersQuery, int>, StreamFilterEvenBehavior>();

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();

        var query = new StreamNumbersQuery(10);
        var results = new List<int>();

        // Act
        await foreach (var item in dispatcher.Stream(query))
        {
            results.Add(item);
        }

        // Assert - Only even numbers
        Assert.That(results, Is.EqualTo(new[] { 0, 2, 4, 6, 8 }));
    }

    [Test]
    public async Task Stream_WithMultipleBehaviors_AppliesBehaviorsInOrder()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBbQMediator(typeof(StreamNumbersQuery).Assembly);
        services.AddTransient<IStreamHandler<StreamNumbersQuery, int>, StreamNumbersQueryHandler>();
        services.AddTransient<IStreamPipelineBehavior<StreamNumbersQuery, int>, StreamMultiplyBehavior>();
        services.AddTransient<IStreamPipelineBehavior<StreamNumbersQuery, int>, StreamAddBehavior>();

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();

        var query = new StreamNumbersQuery(3);
        var results = new List<int>();

        // Act
        await foreach (var item in dispatcher.Stream(query))
        {
            results.Add(item);
        }

        // Assert - First registered (Multiply) wraps second registered (Add) wraps handler
        // Flow: Handler produces 0,1,2 → Add produces 1,2,3 → Multiply produces 2,4,6
        Assert.That(results, Is.EqualTo(new[] { 2, 4, 6 }));
    }

    [Test]
    public async Task Stream_WithCancellation_StopsStream()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBbQMediator(typeof(StreamNumbersQuery).Assembly);
        services.AddTransient<IStreamHandler<StreamNumbersQuery, int>, StreamNumbersQueryHandler>();

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();

        var query = new StreamNumbersQuery(100);
        var results = new List<int>();
        using var cts = new CancellationTokenSource();

        // Act & Assert
        try
        {
            await foreach (var item in dispatcher.Stream(query, cts.Token))
            {
                results.Add(item);
                if (item >= 4)
                {
                    cts.Cancel();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation occurs
        }

        // Assert
        Assert.That(results, Has.Count.GreaterThanOrEqualTo(5));
        Assert.That(results, Has.Count.LessThanOrEqualTo(6)); // May get one more item before cancellation
    }

    [Test]
    public async Task Stream_WithEmptyResult_ReturnsNoItems()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBbQMediator(typeof(StreamNumbersQuery).Assembly);
        services.AddTransient<IStreamHandler<StreamNumbersQuery, int>, StreamNumbersQueryHandler>();

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();

        var query = new StreamNumbersQuery(0);
        var results = new List<int>();

        // Act
        await foreach (var item in dispatcher.Stream(query))
        {
            results.Add(item);
        }

        // Assert
        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task Stream_WithComplexType_ReturnsComplexItems()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBbQMediator(typeof(StreamUsersQuery).Assembly);
        services.AddTransient<IStreamHandler<StreamUsersQuery, User>, StreamUsersQueryHandler>();

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();

        var query = new StreamUsersQuery();
        var results = new List<User>();

        // Act
        await foreach (var user in dispatcher.Stream(query))
        {
            results.Add(user);
        }

        // Assert
        Assert.That(results, Has.Count.EqualTo(3));
        Assert.That(results[0].Name, Is.EqualTo("User 1"));
        Assert.That(results[1].Name, Is.EqualTo("User 2"));
        Assert.That(results[2].Name, Is.EqualTo("User 3"));
    }
}

// Test streaming queries
public record StreamNumbersQuery(int Count) : IStreamQuery<int>;
public record StreamUsersQuery : IStreamQuery<User>;

// Test data class
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

// Test stream handler implementations
public class StreamNumbersQueryHandler : IStreamHandler<StreamNumbersQuery, int>
{
    public async IAsyncEnumerable<int> Handle(
        StreamNumbersQuery request, 
        [EnumeratorCancellation] CancellationToken ct)
    {
        for (int i = 0; i < request.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(1, ct); // Simulate async work
            yield return i;
        }
    }
}

public class StreamUsersQueryHandler : IStreamHandler<StreamUsersQuery, User>
{
    public async IAsyncEnumerable<User> Handle(
        StreamUsersQuery request, 
        [EnumeratorCancellation] CancellationToken ct)
    {
        for (int i = 1; i <= 3; i++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(1, ct);
            yield return new User { Id = i, Name = $"User {i}" };
        }
    }
}

// Test behaviors
public class StreamLoggingBehavior : IStreamPipelineBehavior<StreamNumbersQuery, int>
{
    public int ItemCount { get; private set; }

    public async IAsyncEnumerable<int> Handle(
        StreamNumbersQuery request,
        [EnumeratorCancellation] CancellationToken ct,
        Func<StreamNumbersQuery, CancellationToken, IAsyncEnumerable<int>> next)
    {
        ItemCount = 0;
        await foreach (var item in next(request, ct).WithCancellation(ct))
        {
            ItemCount++;
            yield return item;
        }
    }
}

public class StreamFilterEvenBehavior : IStreamPipelineBehavior<StreamNumbersQuery, int>
{
    public async IAsyncEnumerable<int> Handle(
        StreamNumbersQuery request,
        [EnumeratorCancellation] CancellationToken ct,
        Func<StreamNumbersQuery, CancellationToken, IAsyncEnumerable<int>> next)
    {
        await foreach (var item in next(request, ct).WithCancellation(ct))
        {
            if (item % 2 == 0)
            {
                yield return item;
            }
        }
    }
}

public class StreamMultiplyBehavior : IStreamPipelineBehavior<StreamNumbersQuery, int>
{
    public async IAsyncEnumerable<int> Handle(
        StreamNumbersQuery request,
        [EnumeratorCancellation] CancellationToken ct,
        Func<StreamNumbersQuery, CancellationToken, IAsyncEnumerable<int>> next)
    {
        await foreach (var item in next(request, ct).WithCancellation(ct))
        {
            yield return item * 2;
        }
    }
}

public class StreamAddBehavior : IStreamPipelineBehavior<StreamNumbersQuery, int>
{
    public async IAsyncEnumerable<int> Handle(
        StreamNumbersQuery request,
        [EnumeratorCancellation] CancellationToken ct,
        Func<StreamNumbersQuery, CancellationToken, IAsyncEnumerable<int>> next)
    {
        await foreach (var item in next(request, ct).WithCancellation(ct))
        {
            yield return item + 1;
        }
    }
}
