using BbQ.Events.Checkpointing;
using BbQ.Events.Configuration;
using BbQ.Events.Engine;
using BbQ.Events.Events;
using BbQ.Events.Projections;

using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Integration tests for replay service with event streaming.
/// </summary>
[TestFixture]
public class ReplayEventStreamingTests
{
    private ServiceProvider _serviceProvider = null!;
    private IReplayService _replayService = null!;
    private IProjectionCheckpointStore _checkpointStore = null!;
    private InMemoryEventStore _eventStore = null!;

    [SetUp]
    public void Setup()
    {
        // Clear static test data
        TestEventStreamingProjection.ProcessedEvents.Clear();
        
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        
        // Register event store for replay
        _eventStore = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(_eventStore);
        
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
    public async Task ReplayAsync_WithEventStore_StreamsAndProcessesEvents()
    {
        // Arrange
        TestEventStreamingProjection.ProcessedEvents.Clear();
        
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        
        var eventStore = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(eventStore);
        
        services.AddProjection<TestEventStreamingProjection>();
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        var replayService = provider.GetRequiredService<IReplayService>();

        // Seed events in the event store
        var streamName = "TestEventStreamingProjection";
        await eventStore.AppendAsync(streamName, new UserCreatedEvent("user-1", "Alice", "alice@test.com"));
        await eventStore.AppendAsync(streamName, new UserCreatedEvent("user-2", "Bob", "bob@test.com"));
        await eventStore.AppendAsync(streamName, new UserCreatedEvent("user-3", "Charlie", "charlie@test.com"));

        var options = new ReplayOptions
        {
            FromPosition = 0,
            BatchSize = 10
        };

        // Act
        await replayService.ReplayAsync("TestEventStreamingProjection", options, CancellationToken.None);

        // Assert
        Assert.That(TestEventStreamingProjection.ProcessedEvents.Count, Is.EqualTo(3));
        Assert.That(TestEventStreamingProjection.ProcessedEvents[0].Name, Is.EqualTo("Alice"));
        Assert.That(TestEventStreamingProjection.ProcessedEvents[1].Name, Is.EqualTo("Bob"));
        Assert.That(TestEventStreamingProjection.ProcessedEvents[2].Name, Is.EqualTo("Charlie"));

        provider.Dispose();
    }

    [Test]
    public async Task ReplayAsync_WithToPosition_StopsAtCorrectPosition()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        
        var eventStore = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(eventStore);
        
        services.AddProjection<TestEventStreamingProjection>();
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        var replayService = provider.GetRequiredService<IReplayService>();

        // Seed events in the event store
        var streamName = "TestEventStreamingProjection";
        await eventStore.AppendAsync(streamName, new UserCreatedEvent("user-1", "Alice", "alice@test.com"));
        await eventStore.AppendAsync(streamName, new UserCreatedEvent("user-2", "Bob", "bob@test.com"));
        await eventStore.AppendAsync(streamName, new UserCreatedEvent("user-3", "Charlie", "charlie@test.com"));
        await eventStore.AppendAsync(streamName, new UserCreatedEvent("user-4", "David", "david@test.com"));

        var options = new ReplayOptions
        {
            FromPosition = 0,
            ToPosition = 1, // Only process first 2 events (positions 0 and 1)
            BatchSize = 10
        };

        // Act
        await replayService.ReplayAsync("TestEventStreamingProjection", options, CancellationToken.None);

        // Assert - Should only process 2 events
        Assert.That(TestEventStreamingProjection.ProcessedEvents.Count, Is.EqualTo(2));
        Assert.That(TestEventStreamingProjection.ProcessedEvents[0].Name, Is.EqualTo("Alice"));
        Assert.That(TestEventStreamingProjection.ProcessedEvents[1].Name, Is.EqualTo("Bob"));

        provider.Dispose();
    }

    [Test]
    public async Task ReplayAsync_WithFromPosition_StartsAtCorrectPosition()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        
        var eventStore = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(eventStore);
        
        services.AddProjection<TestEventStreamingProjection>();
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        var replayService = provider.GetRequiredService<IReplayService>();

        // Seed events in the event store
        var streamName = "TestEventStreamingProjection";
        await eventStore.AppendAsync(streamName, new UserCreatedEvent("user-1", "Alice", "alice@test.com"));
        await eventStore.AppendAsync(streamName, new UserCreatedEvent("user-2", "Bob", "bob@test.com"));
        await eventStore.AppendAsync(streamName, new UserCreatedEvent("user-3", "Charlie", "charlie@test.com"));

        var options = new ReplayOptions
        {
            FromPosition = 1, // Start from second event
            BatchSize = 10
        };

        // Act
        await replayService.ReplayAsync("TestEventStreamingProjection", options, CancellationToken.None);

        // Assert - Should skip first event
        Assert.That(TestEventStreamingProjection.ProcessedEvents.Count, Is.EqualTo(2));
        Assert.That(TestEventStreamingProjection.ProcessedEvents[0].Name, Is.EqualTo("Bob"));
        Assert.That(TestEventStreamingProjection.ProcessedEvents[1].Name, Is.EqualTo("Charlie"));

        provider.Dispose();
    }

    [Test]
    public async Task ReplayAsync_WithNormalCheckpointing_WritesCheckpointsInBatches()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        
        var eventStore = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(eventStore);
        
        services.AddProjection<TestEventStreamingProjection>();
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        var replayService = provider.GetRequiredService<IReplayService>();
        var checkpointStore = provider.GetRequiredService<IProjectionCheckpointStore>();

        // Seed events in the event store
        var streamName = "TestEventStreamingProjection";
        for (int i = 0; i < 5; i++)
        {
            await eventStore.AppendAsync(streamName, new UserCreatedEvent($"user-{i}", $"User{i}", $"user{i}@test.com"));
        }

        var options = new ReplayOptions
        {
            FromPosition = 0,
            BatchSize = 2, // Checkpoint every 2 events
            CheckpointMode = CheckpointMode.Normal
        };

        // Act
        await replayService.ReplayAsync("TestEventStreamingProjection", options, CancellationToken.None);

        // Assert - Final checkpoint should be at position 4 (last event)
        var checkpoint = await checkpointStore.GetCheckpointAsync("TestEventStreamingProjection", CancellationToken.None);
        Assert.That(checkpoint, Is.EqualTo(4));

        provider.Dispose();
    }

    [Test]
    public async Task ReplayAsync_WithFinalOnlyCheckpointing_WritesOnlyFinalCheckpoint()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        
        var eventStore = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(eventStore);
        
        services.AddProjection<TestEventStreamingProjection>();
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        var replayService = provider.GetRequiredService<IReplayService>();
        var checkpointStore = provider.GetRequiredService<IProjectionCheckpointStore>();

        // Seed events in the event store
        var streamName = "TestEventStreamingProjection";
        await eventStore.AppendAsync(streamName, new UserCreatedEvent("user-1", "Alice", "alice@test.com"));
        await eventStore.AppendAsync(streamName, new UserCreatedEvent("user-2", "Bob", "bob@test.com"));
        await eventStore.AppendAsync(streamName, new UserCreatedEvent("user-3", "Charlie", "charlie@test.com"));

        var options = new ReplayOptions
        {
            FromPosition = 0,
            BatchSize = 1, // Would checkpoint after each event in Normal mode
            CheckpointMode = CheckpointMode.FinalOnly
        };

        // Act
        await replayService.ReplayAsync("TestEventStreamingProjection", options, CancellationToken.None);

        // Assert - Checkpoint should be at final position
        var checkpoint = await checkpointStore.GetCheckpointAsync("TestEventStreamingProjection", CancellationToken.None);
        Assert.That(checkpoint, Is.EqualTo(2)); // Position of last event

        provider.Dispose();
    }

    [Test]
    public async Task ReplayAsync_WithDryRun_DoesNotWriteCheckpoints()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        
        var eventStore = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(eventStore);
        
        services.AddProjection<TestEventStreamingProjection>();
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        var replayService = provider.GetRequiredService<IReplayService>();
        var checkpointStore = provider.GetRequiredService<IProjectionCheckpointStore>();

        // Seed events in the event store
        var streamName = "TestEventStreamingProjection";
        await eventStore.AppendAsync(streamName, new UserCreatedEvent("user-1", "Alice", "alice@test.com"));
        await eventStore.AppendAsync(streamName, new UserCreatedEvent("user-2", "Bob", "bob@test.com"));

        var options = new ReplayOptions
        {
            FromPosition = 0,
            DryRun = true
        };

        // Act
        await replayService.ReplayAsync("TestEventStreamingProjection", options, CancellationToken.None);

        // Assert - No checkpoint should be written
        var checkpoint = await checkpointStore.GetCheckpointAsync("TestEventStreamingProjection", CancellationToken.None);
        Assert.That(checkpoint, Is.Null);

        // But events should still be processed
        Assert.That(TestEventStreamingProjection.ProcessedEvents.Count, Is.EqualTo(2));

        provider.Dispose();
    }

    [Test]
    public async Task ReplayAsync_WithoutEventStore_LogsWarningAndReturns()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        
        // Note: NOT registering IEventStore
        services.AddProjection<TestEventStreamingProjection>();
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        var replayService = provider.GetRequiredService<IReplayService>();

        var options = new ReplayOptions
        {
            FromPosition = 0
        };

        // Act - Should not throw, just log warning
        await replayService.ReplayAsync("TestEventStreamingProjection", options, CancellationToken.None);

        // Assert - No events should be processed
        Assert.That(TestEventStreamingProjection.ProcessedEvents.Count, Is.EqualTo(0));

        provider.Dispose();
    }

    // Test event and projection
    public record UserCreatedEvent(string UserId, string Name, string Email);

    [Projection]
    public class TestEventStreamingProjection : IProjectionHandler<UserCreatedEvent>
    {
        public static List<UserCreatedEvent> ProcessedEvents { get; } = new();

        public ValueTask ProjectAsync(UserCreatedEvent @event, CancellationToken ct = default)
        {
            ProcessedEvents.Add(@event);
            return ValueTask.CompletedTask;
        }
    }
}
