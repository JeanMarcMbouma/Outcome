using BbQ.Events.Checkpointing;
using BbQ.Events.Configuration;
using BbQ.Events.Engine;
using BbQ.Events.Events;
using BbQ.Events.Projections;

using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Collections.Concurrent;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Tests for the projection service with batch processing, parallel processing,
/// and automatic checkpointing.
/// </summary>
[TestFixture]
public class ProjectionServiceTests
{
    /// <summary>Time to wait for the service to process dispatched events.</summary>
    private const int ProcessingDelayMs = 1000;
    
    /// <summary>Time to wait for the batch timeout to trigger partial batch dispatch.</summary>
    private const int BatchTimeoutWaitMs = 500;

    [TearDown]
    public void TearDown()
    {
        ProjectionHandlerRegistry.Clear();
    }

    [Test]
    public void AddBatchProjection_RegistersBatchHandler()
    {
        // Arrange
        ProjectionHandlerRegistry.Clear();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();

        // Act
        services.AddBatchProjection<TestBatchProjection>();
        var provider = services.BuildServiceProvider();

        // Assert
        var projection = provider.GetService<TestBatchProjection>();
        Assert.That(projection, Is.Not.Null);

        var handler = provider.GetService<IProjectionBatchHandler<BatchTestEvent>>();
        Assert.That(handler, Is.Not.Null);
        Assert.That(handler, Is.InstanceOf<TestBatchProjection>());
    }

    [Test]
    public void AddBatchProjection_WithOptions_ConfiguresOptions()
    {
        // Arrange
        ProjectionHandlerRegistry.Clear();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();

        // Act
        services.AddBatchProjection<TestBatchProjection>(options =>
        {
            options.BatchSize = 50;
            options.BatchTimeout = TimeSpan.FromSeconds(10);
            options.MaxDegreeOfParallelism = 4;
            options.AutoCheckpoint = true;
        });

        // Assert
        var options = ProjectionHandlerRegistry.GetProjectionServiceOptions(nameof(TestBatchProjection));
        Assert.That(options, Is.Not.Null);
        Assert.That(options!.BatchSize, Is.EqualTo(50));
        Assert.That(options.BatchTimeout, Is.EqualTo(TimeSpan.FromSeconds(10)));
        Assert.That(options.MaxDegreeOfParallelism, Is.EqualTo(4));
        Assert.That(options.AutoCheckpoint, Is.True);
    }

    [Test]
    public void AddProjectionService_RegistersServiceAndCheckpointStore()
    {
        // Arrange
        ProjectionHandlerRegistry.Clear();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();

        // Act
        services.AddProjectionService();
        var provider = services.BuildServiceProvider();

        // Assert
        var service = provider.GetService<IProjectionService>();
        var checkpointStore = provider.GetService<IProjectionCheckpointStore>();

        Assert.That(service, Is.Not.Null);
        Assert.That(checkpointStore, Is.Not.Null);
        Assert.That(checkpointStore, Is.InstanceOf<InMemoryProjectionCheckpointStore>());
    }

    [Test]
    public async Task BatchHandler_ProjectsBatchCorrectly()
    {
        // Arrange
        ProjectionHandlerRegistry.Clear();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddBatchProjection<TestBatchProjection>();

        var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IProjectionBatchHandler<BatchTestEvent>>();

        var events = new List<BatchTestEvent>
        {
            new("1", "Alice"),
            new("2", "Bob"),
            new("3", "Charlie")
        };

        // Act
        await handler.ProjectBatchAsync(events);

        // Assert
        var projection = (TestBatchProjection)handler;
        Assert.That(projection.ProcessedBatches.Count, Is.EqualTo(1));
        Assert.That(projection.ProcessedBatches[0].Count, Is.EqualTo(3));
        Assert.That(projection.TotalEventsProcessed, Is.EqualTo(3));
    }

    [Test]
    public async Task BatchHandler_ProcessesMultipleBatches()
    {
        // Arrange
        ProjectionHandlerRegistry.Clear();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddBatchProjection<TestBatchProjection>();

        var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IProjectionBatchHandler<BatchTestEvent>>();

        // Act - Process two batches
        await handler.ProjectBatchAsync(new List<BatchTestEvent>
        {
            new("1", "Alice"),
            new("2", "Bob")
        });

        await handler.ProjectBatchAsync(new List<BatchTestEvent>
        {
            new("3", "Charlie"),
            new("4", "Dave"),
            new("5", "Eve")
        });

        // Assert
        var projection = (TestBatchProjection)handler;
        Assert.That(projection.ProcessedBatches.Count, Is.EqualTo(2));
        Assert.That(projection.ProcessedBatches[0].Count, Is.EqualTo(2));
        Assert.That(projection.ProcessedBatches[1].Count, Is.EqualTo(3));
        Assert.That(projection.TotalEventsProcessed, Is.EqualTo(5));
    }

    [Test]
    public void ProjectionServiceOptions_HasCorrectDefaults()
    {
        // Arrange & Act
        var options = new ProjectionServiceOptions();

        // Assert
        Assert.That(options.BatchSize, Is.EqualTo(100));
        Assert.That(options.BatchTimeout, Is.EqualTo(TimeSpan.FromSeconds(5)));
        Assert.That(options.MaxDegreeOfParallelism, Is.EqualTo(1));
        Assert.That(options.AutoCheckpoint, Is.True);
        Assert.That(options.ChannelCapacity, Is.EqualTo(1000));
        Assert.That(options.StartupMode, Is.EqualTo(ProjectionStartupMode.Resume));
        Assert.That(options.ProjectionName, Is.EqualTo(string.Empty));
    }

    [Test]
    public async Task ProjectionService_ProcessesBatchEvents_WithAutomaticCheckpointing()
    {
        // Arrange
        ProjectionHandlerRegistry.Clear();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddBatchProjection<TestBatchProjection>(options =>
        {
            options.BatchSize = 3;
            options.BatchTimeout = TimeSpan.FromMilliseconds(200);
            options.AutoCheckpoint = true;
        });
        services.AddProjectionService();

        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();
        var service = provider.GetRequiredService<IProjectionService>();
        var checkpointStore = provider.GetRequiredService<IProjectionCheckpointStore>();

        // Act - Start service and publish events
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var serviceTask = service.RunAsync(cts.Token);

        // Publish events that will fill a batch
        for (int i = 0; i < 3; i++)
        {
            await eventBus.Publish(new BatchTestEvent(i.ToString(), $"User-{i}"), cts.Token);
        }

        // Wait for processing
        await Task.Delay(ProcessingDelayMs, CancellationToken.None);

        // Cancel and wait for graceful shutdown
        cts.Cancel();

        try
        {
            await serviceTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - Verify checkpoint was saved (automatic checkpointing)
        var checkpoint = await checkpointStore.GetCheckpointAsync("TestBatchProjection:_default");
        Assert.That(checkpoint, Is.Not.Null, "Checkpoint should have been saved automatically");
        Assert.That(checkpoint!.Value, Is.GreaterThan(0), "Checkpoint should be greater than 0");
    }

    [Test]
    public async Task ProjectionService_FlushesRemainingEventsOnShutdown()
    {
        // Arrange
        ProjectionHandlerRegistry.Clear();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddBatchProjection<TestBatchProjection>(options =>
        {
            options.BatchSize = 100; // Large batch size - won't fill naturally
            options.BatchTimeout = TimeSpan.FromMilliseconds(100); // Short timeout
            options.AutoCheckpoint = true;
        });
        services.AddProjectionService();

        var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();
        var service = provider.GetRequiredService<IProjectionService>();

        // Act - Start service and publish a few events (less than batch size)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var serviceTask = service.RunAsync(cts.Token);

        // Publish events that won't fill a batch
        await eventBus.Publish(new BatchTestEvent("1", "Alice"), cts.Token);
        await eventBus.Publish(new BatchTestEvent("2", "Bob"), cts.Token);

        // Wait for batch timeout to trigger
        await Task.Delay(BatchTimeoutWaitMs, CancellationToken.None);

        // Cancel to trigger shutdown
        cts.Cancel();

        try
        {
            await serviceTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - Verify remaining events were flushed
        var checkpointStore = provider.GetRequiredService<IProjectionCheckpointStore>();
        var checkpoint = await checkpointStore.GetCheckpointAsync("TestBatchProjection:_default");
        // Checkpoint should exist from timeout-triggered batch or shutdown flush
        Assert.That(checkpoint, Is.Not.Null, "Checkpoint should have been saved for flushed batch");
    }

    [Test]
    public void AddBatchProjection_WithMultipleEventTypes_RegistersAllHandlers()
    {
        // Arrange
        ProjectionHandlerRegistry.Clear();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();

        // Act
        services.AddBatchProjection<TestMultiEventBatchProjection>();
        var provider = services.BuildServiceProvider();

        // Assert
        var handler1 = provider.GetService<IProjectionBatchHandler<BatchTestEvent>>();
        var handler2 = provider.GetService<IProjectionBatchHandler<BatchTestEvent2>>();

        Assert.That(handler1, Is.Not.Null);
        Assert.That(handler2, Is.Not.Null);

        // Both should resolve to the same projection instance
        using var scope = provider.CreateScope();
        var h1 = scope.ServiceProvider.GetRequiredService<IProjectionBatchHandler<BatchTestEvent>>();
        var h2 = scope.ServiceProvider.GetRequiredService<IProjectionBatchHandler<BatchTestEvent2>>();
        Assert.That(h1, Is.SameAs(h2));
    }

    [Test]
    public void ProjectionService_WithNoHandlers_ReturnsImmediately()
    {
        // Arrange
        ProjectionHandlerRegistry.Clear();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddProjectionService();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IProjectionService>();

        // Act & Assert - Should complete quickly without handlers
        var task = service.RunAsync(CancellationToken.None);
        Assert.That(task.Wait(TimeSpan.FromSeconds(2)), Is.True, "Service should complete when no handlers are registered");
    }

    [Test]
    public void AddProjectionsFromAssembly_DiscoversBatchHandlers()
    {
        // Arrange
        ProjectionHandlerRegistry.Clear();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();

        // Act
        services.AddProjectionsFromAssembly(typeof(ProjectionServiceTests).Assembly);
        var provider = services.BuildServiceProvider();

        // Assert - TestDiscoverableBatchProjection has [Projection] attribute
        var projection = provider.GetService<TestDiscoverableBatchProjection>();
        Assert.That(projection, Is.Not.Null);

        var handler = provider.GetService<IProjectionBatchHandler<BatchTestEvent>>();
        Assert.That(handler, Is.Not.Null);
    }

    [Test]
    public void ProjectionHandlerRegistry_Clear_ClearsServiceOptions()
    {
        // Arrange
        ProjectionHandlerRegistry.Clear();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddBatchProjection<TestBatchProjection>(options =>
        {
            options.BatchSize = 50;
        });

        // Verify options exist
        var options = ProjectionHandlerRegistry.GetProjectionServiceOptions(nameof(TestBatchProjection));
        Assert.That(options, Is.Not.Null);

        // Act
        ProjectionHandlerRegistry.Clear();

        // Assert
        options = ProjectionHandlerRegistry.GetProjectionServiceOptions(nameof(TestBatchProjection));
        Assert.That(options, Is.Null);
    }

    #region Test Event Types

    public record BatchTestEvent(string Id, string Name);
    public record BatchTestEvent2(string Id, string Value);

    #endregion

    #region Test Projections

    /// <summary>
    /// Simple batch projection for testing.
    /// </summary>
    public class TestBatchProjection : IProjectionBatchHandler<BatchTestEvent>
    {
        private readonly List<List<BatchTestEvent>> _processedBatches = new();
        public IReadOnlyList<List<BatchTestEvent>> ProcessedBatches => _processedBatches;
        public int TotalEventsProcessed => _processedBatches.Sum(b => b.Count);

        public ValueTask ProjectBatchAsync(IReadOnlyList<BatchTestEvent> events, CancellationToken ct = default)
        {
            _processedBatches.Add(new List<BatchTestEvent>(events));
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Multi-event batch projection for testing.
    /// </summary>
    public class TestMultiEventBatchProjection :
        IProjectionBatchHandler<BatchTestEvent>,
        IProjectionBatchHandler<BatchTestEvent2>
    {
        public int EventBatchCount { get; private set; }
        public int Event2BatchCount { get; private set; }

        public ValueTask ProjectBatchAsync(IReadOnlyList<BatchTestEvent> events, CancellationToken ct = default)
        {
            EventBatchCount += events.Count;
            return ValueTask.CompletedTask;
        }

        public ValueTask ProjectBatchAsync(IReadOnlyList<BatchTestEvent2> events, CancellationToken ct = default)
        {
            Event2BatchCount += events.Count;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Discoverable batch projection with [Projection] attribute for assembly scanning tests.
    /// </summary>
    [Projection]
    public class TestDiscoverableBatchProjection : IProjectionBatchHandler<BatchTestEvent>
    {
        public ValueTask ProjectBatchAsync(IReadOnlyList<BatchTestEvent> events, CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }
    }

    #endregion
}
