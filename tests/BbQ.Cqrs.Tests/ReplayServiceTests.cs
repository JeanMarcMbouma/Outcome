using BbQ.Events.Checkpointing;
using BbQ.Events.Configuration;
using BbQ.Events.Engine;
using BbQ.Events.Events;
using BbQ.Events.Projections;

using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Tests for replay service functionality.
/// </summary>
[TestFixture]
public class ReplayServiceTests
{
    private ServiceProvider _serviceProvider = null!;
    private IReplayService _replayService = null!;
    private IProjectionCheckpointStore _checkpointStore = null!;

    [SetUp]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddProjectionEngine();
        
        _serviceProvider = services.BuildServiceProvider();
        _replayService = _serviceProvider.GetRequiredService<IReplayService>();
        _checkpointStore = _serviceProvider.GetRequiredService<IProjectionCheckpointStore>();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider?.Dispose();
        ProjectionHandlerRegistry.Clear();
    }

    [Test]
    public void AddProjectionEngine_RegistersReplayService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();

        // Act
        services.AddProjectionEngine();
        var provider = services.BuildServiceProvider();

        // Assert
        var replayService = provider.GetService<IReplayService>();
        Assert.That(replayService, Is.Not.Null);
        
        provider.Dispose();
    }

    [Test]
    public void ReplayAsync_WithNullProjectionName_ThrowsArgumentException()
    {
        // Arrange
        var options = new ReplayOptions();

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _replayService.ReplayAsync(null!, options, CancellationToken.None));
    }

    [Test]
    public void ReplayAsync_WithEmptyProjectionName_ThrowsArgumentException()
    {
        // Arrange
        var options = new ReplayOptions();

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _replayService.ReplayAsync("", options, CancellationToken.None));
    }

    [Test]
    public void ReplayAsync_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _replayService.ReplayAsync("TestProjection", null!, CancellationToken.None));
    }

    [Test]
    public void ReplayAsync_WithUnregisteredProjection_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new ReplayOptions { FromPosition = 0 };

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _replayService.ReplayAsync("NonExistentProjection", options, CancellationToken.None));
        
        Assert.That(ex!.Message, Does.Contain("is not registered"));
    }

    [Test]
    public async Task ReplayAsync_WithFromPosition_ResetsCheckpoint()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddProjection<TestUserProfileProjection>();
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        var replayService = provider.GetRequiredService<IReplayService>();
        var checkpointStore = provider.GetRequiredService<IProjectionCheckpointStore>();

        // Save a checkpoint
        await checkpointStore.SaveCheckpointAsync("TestUserProfileProjection", 100, CancellationToken.None);
        
        var checkpointBefore = await checkpointStore.GetCheckpointAsync("TestUserProfileProjection", CancellationToken.None);
        Assert.That(checkpointBefore, Is.EqualTo(100));

        var options = new ReplayOptions { FromPosition = 0 };

        // Act
        await replayService.ReplayAsync("TestUserProfileProjection", options, CancellationToken.None);

        // Assert - Checkpoint should be reset when not resuming from checkpoint
        var checkpointAfter = await checkpointStore.GetCheckpointAsync("TestUserProfileProjection", CancellationToken.None);
        Assert.That(checkpointAfter, Is.Null);

        provider.Dispose();
    }

    [Test]
    public async Task ReplayAsync_WithFromCheckpoint_DoesNotResetCheckpoint()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddProjection<TestUserProfileProjection>();
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        var replayService = provider.GetRequiredService<IReplayService>();
        var checkpointStore = provider.GetRequiredService<IProjectionCheckpointStore>();

        // Save a checkpoint
        await checkpointStore.SaveCheckpointAsync("TestUserProfileProjection", 100, CancellationToken.None);

        var options = new ReplayOptions { FromCheckpoint = true };

        // Act
        await replayService.ReplayAsync("TestUserProfileProjection", options, CancellationToken.None);

        // Assert - Checkpoint should NOT be reset when resuming from checkpoint
        var checkpointAfter = await checkpointStore.GetCheckpointAsync("TestUserProfileProjection", CancellationToken.None);
        Assert.That(checkpointAfter, Is.EqualTo(100));

        provider.Dispose();
    }

    [Test]
    public async Task ReplayAsync_WithDryRun_DoesNotResetCheckpoint()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddProjection<TestUserProfileProjection>();
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        var replayService = provider.GetRequiredService<IReplayService>();
        var checkpointStore = provider.GetRequiredService<IProjectionCheckpointStore>();

        // Save a checkpoint
        await checkpointStore.SaveCheckpointAsync("TestUserProfileProjection", 100, CancellationToken.None);

        var options = new ReplayOptions 
        { 
            FromPosition = 0,
            DryRun = true 
        };

        // Act
        await replayService.ReplayAsync("TestUserProfileProjection", options, CancellationToken.None);

        // Assert - Checkpoint should NOT be reset in dry run mode
        var checkpointAfter = await checkpointStore.GetCheckpointAsync("TestUserProfileProjection", CancellationToken.None);
        Assert.That(checkpointAfter, Is.EqualTo(100));

        provider.Dispose();
    }

    [Test]
    public async Task ReplayAsync_WithPartition_UsesPartitionedCheckpointKey()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddProjection<TestPartitionedProjection>();
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        var replayService = provider.GetRequiredService<IReplayService>();
        var checkpointStore = provider.GetRequiredService<IProjectionCheckpointStore>();

        var partitionKey = "partition-1";
        var checkpointKey = $"TestPartitionedProjection:{partitionKey}";
        
        // Save a checkpoint for the partition
        await checkpointStore.SaveCheckpointAsync(checkpointKey, 100, CancellationToken.None);

        var options = new ReplayOptions 
        { 
            FromPosition = 0,
            Partition = partitionKey
        };

        // Act
        await replayService.ReplayAsync("TestPartitionedProjection", options, CancellationToken.None);

        // Assert - Partitioned checkpoint should be reset
        var checkpointAfter = await checkpointStore.GetCheckpointAsync(checkpointKey, CancellationToken.None);
        Assert.That(checkpointAfter, Is.Null);

        provider.Dispose();
    }

    [Test]
    public async Task ReplayAsync_ValidatesReplayOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddProjection<TestUserProfileProjection>();
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        var replayService = provider.GetRequiredService<IReplayService>();

        // Test case 1: FromPosition > ToPosition should throw
        var options1 = new ReplayOptions 
        { 
            FromPosition = 100,
            ToPosition = 50
        };

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await replayService.ReplayAsync("TestUserProfileProjection", options1, CancellationToken.None));

        // Test case 2: Negative FromPosition should throw
        var options2 = new ReplayOptions 
        { 
            FromPosition = -1
        };

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await replayService.ReplayAsync("TestUserProfileProjection", options2, CancellationToken.None));

        // Test case 3: Negative ToPosition should throw
        var options3 = new ReplayOptions 
        { 
            ToPosition = -1
        };

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await replayService.ReplayAsync("TestUserProfileProjection", options3, CancellationToken.None));

        // Test case 4: Non-positive BatchSize should throw
        var options4 = new ReplayOptions 
        { 
            BatchSize = 0
        };

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await replayService.ReplayAsync("TestUserProfileProjection", options4, CancellationToken.None));

        provider.Dispose();
    }

    [Test]
    public async Task ReplayAsync_WithCheckpointModeNone_DoesNotResetCheckpoint()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddProjection<TestUserProfileProjection>();
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        var replayService = provider.GetRequiredService<IReplayService>();
        var checkpointStore = provider.GetRequiredService<IProjectionCheckpointStore>();

        // Save a checkpoint
        await checkpointStore.SaveCheckpointAsync("TestUserProfileProjection", 100, CancellationToken.None);

        var options = new ReplayOptions 
        { 
            FromPosition = 0,
            CheckpointMode = CheckpointMode.None
        };

        // Act
        await replayService.ReplayAsync("TestUserProfileProjection", options, CancellationToken.None);

        // Assert - Checkpoint should NOT be reset when CheckpointMode is None
        var checkpointAfter = await checkpointStore.GetCheckpointAsync("TestUserProfileProjection", CancellationToken.None);
        Assert.That(checkpointAfter, Is.EqualTo(100));

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
    public class TestPartitionedProjection : IPartitionedProjectionHandler<UserActivityEvent>
    {
        public string GetPartitionKey(UserActivityEvent @event)
        {
            return @event.UserId;
        }

        public ValueTask ProjectAsync(UserActivityEvent @event, CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }
    }
}
