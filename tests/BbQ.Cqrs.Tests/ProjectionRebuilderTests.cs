using BbQ.Events;
using BbQ.Events.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Tests for projection rebuilder functionality.
/// </summary>
[TestFixture]
public class ProjectionRebuilderTests
{
    private ServiceProvider _serviceProvider = null!;
    private IProjectionRebuilder _rebuilder = null!;
    private IProjectionCheckpointStore _checkpointStore = null!;

    [SetUp]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddProjectionEngine();
        
        _serviceProvider = services.BuildServiceProvider();
        _rebuilder = _serviceProvider.GetRequiredService<IProjectionRebuilder>();
        _checkpointStore = _serviceProvider.GetRequiredService<IProjectionCheckpointStore>();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider?.Dispose();
        ProjectionHandlerRegistry.Clear();
    }

    [Test]
    public void AddProjectionEngine_RegistersRebuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();

        // Act
        services.AddProjectionEngine();
        var provider = services.BuildServiceProvider();

        // Assert
        var rebuilder = provider.GetService<IProjectionRebuilder>();
        Assert.That(rebuilder, Is.Not.Null);
    }

    [Test]
    public async Task ResetProjectionAsync_ResetsCheckpoint()
    {
        // Arrange
        var projectionName = "TestProjection";
        await _checkpointStore.SaveCheckpointAsync(projectionName, 100, CancellationToken.None);

        // Verify checkpoint exists
        var checkpointBefore = await _checkpointStore.GetCheckpointAsync(projectionName, CancellationToken.None);
        Assert.That(checkpointBefore, Is.EqualTo(100));

        // Act
        await _rebuilder.ResetProjectionAsync(projectionName, CancellationToken.None);

        // Assert
        var checkpointAfter = await _checkpointStore.GetCheckpointAsync(projectionName, CancellationToken.None);
        Assert.That(checkpointAfter, Is.Null);
    }

    [Test]
    public void ResetProjectionAsync_WithNullProjectionName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () => 
            await _rebuilder.ResetProjectionAsync(null!, CancellationToken.None));
    }

    [Test]
    public void ResetProjectionAsync_WithEmptyProjectionName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () => 
            await _rebuilder.ResetProjectionAsync("", CancellationToken.None));
    }

    [Test]
    public async Task ResetPartitionAsync_ResetsSpecificPartitionCheckpoint()
    {
        // Arrange
        var projectionName = "TestPartitionedProjection";
        var partitionKey = "partition-1";
        var checkpointKey = $"{projectionName}:{partitionKey}";
        
        await _checkpointStore.SaveCheckpointAsync(checkpointKey, 100, CancellationToken.None);

        // Verify checkpoint exists
        var checkpointBefore = await _checkpointStore.GetCheckpointAsync(checkpointKey, CancellationToken.None);
        Assert.That(checkpointBefore, Is.EqualTo(100));

        // Act
        await _rebuilder.ResetPartitionAsync(projectionName, partitionKey, CancellationToken.None);

        // Assert
        var checkpointAfter = await _checkpointStore.GetCheckpointAsync(checkpointKey, CancellationToken.None);
        Assert.That(checkpointAfter, Is.Null);
    }

    [Test]
    public async Task ResetPartitionAsync_DoesNotAffectOtherPartitions()
    {
        // Arrange
        var projectionName = "TestPartitionedProjection";
        var partition1 = "partition-1";
        var partition2 = "partition-2";
        var checkpointKey1 = $"{projectionName}:{partition1}";
        var checkpointKey2 = $"{projectionName}:{partition2}";
        
        await _checkpointStore.SaveCheckpointAsync(checkpointKey1, 100, CancellationToken.None);
        await _checkpointStore.SaveCheckpointAsync(checkpointKey2, 200, CancellationToken.None);

        // Act - Reset only partition 1
        await _rebuilder.ResetPartitionAsync(projectionName, partition1, CancellationToken.None);

        // Assert
        var checkpoint1 = await _checkpointStore.GetCheckpointAsync(checkpointKey1, CancellationToken.None);
        var checkpoint2 = await _checkpointStore.GetCheckpointAsync(checkpointKey2, CancellationToken.None);
        
        Assert.That(checkpoint1, Is.Null, "Partition 1 checkpoint should be reset");
        Assert.That(checkpoint2, Is.EqualTo(200), "Partition 2 checkpoint should remain unchanged");
    }

    [Test]
    public void ResetPartitionAsync_WithNullProjectionName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () => 
            await _rebuilder.ResetPartitionAsync(null!, "partition-1", CancellationToken.None));
    }

    [Test]
    public void ResetPartitionAsync_WithNullPartitionKey_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () => 
            await _rebuilder.ResetPartitionAsync("TestProjection", null!, CancellationToken.None));
    }

    [Test]
    public async Task ResetAllProjectionsAsync_ResetsAllRegisteredProjections()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddProjection<TestUserProfileProjection>();
        services.AddProjection<TestUserStatisticsProjection>();
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        var rebuilder = provider.GetRequiredService<IProjectionRebuilder>();
        var checkpointStore = provider.GetRequiredService<IProjectionCheckpointStore>();

        // Save checkpoints for projections
        await checkpointStore.SaveCheckpointAsync("TestUserProfileProjection", 100, CancellationToken.None);
        await checkpointStore.SaveCheckpointAsync("TestUserStatisticsProjection", 200, CancellationToken.None);

        // Verify checkpoints exist
        var checkpoint1Before = await checkpointStore.GetCheckpointAsync("TestUserProfileProjection", CancellationToken.None);
        var checkpoint2Before = await checkpointStore.GetCheckpointAsync("TestUserStatisticsProjection", CancellationToken.None);
        Assert.That(checkpoint1Before, Is.EqualTo(100));
        Assert.That(checkpoint2Before, Is.EqualTo(200));

        // Act
        await rebuilder.ResetAllProjectionsAsync(CancellationToken.None);

        // Assert
        var checkpoint1After = await checkpointStore.GetCheckpointAsync("TestUserProfileProjection", CancellationToken.None);
        var checkpoint2After = await checkpointStore.GetCheckpointAsync("TestUserStatisticsProjection", CancellationToken.None);
        Assert.That(checkpoint1After, Is.Null);
        Assert.That(checkpoint2After, Is.Null);

        provider.Dispose();
    }

    [Test]
    public void GetRegisteredProjections_ReturnsEmptyWhenNoProjectionsRegistered()
    {
        // Act
        var projections = _rebuilder.GetRegisteredProjections().ToList();

        // Assert
        Assert.That(projections, Is.Empty);
    }

    [Test]
    public void GetRegisteredProjections_ReturnsRegisteredProjectionNames()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddProjection<TestUserProfileProjection>();
        services.AddProjection<TestUserStatisticsProjection>();
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        var rebuilder = provider.GetRequiredService<IProjectionRebuilder>();

        // Act
        var projections = rebuilder.GetRegisteredProjections().ToList();

        // Assert
        Assert.That(projections, Has.Count.EqualTo(2));
        Assert.That(projections, Does.Contain("TestUserProfileProjection"));
        Assert.That(projections, Does.Contain("TestUserStatisticsProjection"));

        provider.Dispose();
    }

    [Test]
    public void GetRegisteredProjections_ReturnsSortedProjectionNames()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddProjection<TestUserStatisticsProjection>();
        services.AddProjection<TestUserProfileProjection>();
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        var rebuilder = provider.GetRequiredService<IProjectionRebuilder>();

        // Act
        var projections = rebuilder.GetRegisteredProjections().ToList();

        // Assert - Should be sorted alphabetically
        Assert.That(projections, Is.Ordered);
        Assert.That(projections[0], Is.EqualTo("TestUserProfileProjection"));
        Assert.That(projections[1], Is.EqualTo("TestUserStatisticsProjection"));

        provider.Dispose();
    }

    // Test event types
    public record UserCreatedEvent(string UserId, string Name, string Email);
    public record UserActivityEvent(string UserId, string ActivityType);

    // Test projection handlers
    [Projection]
    public class TestUserProfileProjection : IProjectionHandler<UserCreatedEvent>
    {
        public ValueTask ProjectAsync(UserCreatedEvent @event, CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }
    }

    [Projection]
    public class TestUserStatisticsProjection : IProjectionHandler<UserActivityEvent>
    {
        public ValueTask ProjectAsync(UserActivityEvent @event, CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }
    }
}
