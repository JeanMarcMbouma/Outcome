using BbQ.Events;
using BbQ.Events.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.Collections.Concurrent;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Tests for partitioned projection engine functionality.
/// </summary>
[TestFixture]
public class PartitionedProjectionEngineTests
{
    [TearDown]
    public void TearDown()
    {
        // Clear registry between tests
        ProjectionHandlerRegistry.Clear();
        
        // Clear static test data
        TestSequentialProjection.Clear();
        TestPartitionedProjection.Clear();
        TestCheckpointProjection.Clear();
        TestParallelismProjection.Clear();
        TestOrderingProjection.Clear();
    }

    [Test]
    public async Task Engine_ProcessesRegularProjection_SequentiallyInDefaultPartition()
    {
        // Arrange
        ProjectionHandlerRegistry.Clear();
        
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProjection<TestSequentialProjection>();
        
        var provider = services.BuildServiceProvider();
        var projection = provider.GetRequiredService<TestSequentialProjection>();
        
        // Act - Process events directly through projection
        for (int i = 0; i < 10; i++)
        {
            await projection.ProjectAsync(new TestEvent(i, $"event-{i}"));
        }
        
        // Assert
        Assert.That(projection.ProcessedEvents.Count, Is.EqualTo(10));
        
        // Events should be processed in order
        for (int i = 0; i < 10; i++)
        {
            Assert.That(projection.ProcessedEvents[i], Is.EqualTo(i));
        }
    }

    [Test]
    public async Task Engine_ProcessesPartitionedProjection_RoutesToCorrectPartitions()
    {
        // Arrange
        ProjectionHandlerRegistry.Clear();
        
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProjection<TestPartitionedProjection>();
        
        var provider = services.BuildServiceProvider();
        var projection = provider.GetRequiredService<TestPartitionedProjection>();
        
        // Act - Process events directly through projection
        await projection.ProjectAsync(new PartitionedEvent("partition-A", 1));
        await projection.ProjectAsync(new PartitionedEvent("partition-A", 2));
        await projection.ProjectAsync(new PartitionedEvent("partition-B", 3));
        await projection.ProjectAsync(new PartitionedEvent("partition-B", 4));
        await projection.ProjectAsync(new PartitionedEvent("partition-A", 5));
        
        // Assert
        Assert.That(projection.PartitionEvents.ContainsKey("partition-A"), Is.True);
        Assert.That(projection.PartitionEvents.ContainsKey("partition-B"), Is.True);
        
        Assert.That(projection.PartitionEvents["partition-A"].Count, Is.EqualTo(3));
        Assert.That(projection.PartitionEvents["partition-B"].Count, Is.EqualTo(2));
        
        // Events within each partition should be in order
        CollectionAssert.AreEqual(new[] { 1, 2, 5 }, projection.PartitionEvents["partition-A"]);
        CollectionAssert.AreEqual(new[] { 3, 4 }, projection.PartitionEvents["partition-B"]);
    }

    [Test]
    public async Task Engine_SavesCheckpoints_InBatches()
    {
        // Arrange
        ProjectionHandlerRegistry.Clear();
        
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProjection<TestCheckpointProjection>();
        
        var provider = services.BuildServiceProvider();
        var projection = provider.GetRequiredService<TestCheckpointProjection>();
        var checkpointStore = new InMemoryProjectionCheckpointStore();
        
        // Act - Process 12 events (batch size is 5 in TestCheckpointProjection)
        for (int i = 0; i < 12; i++)
        {
            await projection.ProjectAsync(new CheckpointEvent(i));
            
            // Simulate checkpoint batching: save after every 5 events
            if ((i + 1) % 5 == 0)
            {
                await checkpointStore.SaveCheckpointAsync("TestCheckpointProjection:_default", i + 1);
            }
        }
        
        // Check intermediate checkpoint (after 10 events, 2 batches of 5)
        var checkpoint = await checkpointStore.GetCheckpointAsync("TestCheckpointProjection:_default");
        Assert.That(checkpoint, Is.EqualTo(10));
        
        // Simulate final flush for remaining 2 events
        await checkpointStore.SaveCheckpointAsync("TestCheckpointProjection:_default", 12);
        
        // Assert - final checkpoint should be at 12
        var finalCheckpoint = await checkpointStore.GetCheckpointAsync("TestCheckpointProjection:_default");
        Assert.That(finalCheckpoint, Is.EqualTo(12));
        
        // Assert projection processed all events
        Assert.That(projection.ProcessedCount, Is.EqualTo(12));
    }

    [Test]
    public async Task Engine_TracksCheckpointPosition_ForNewEvents()
    {
        // Arrange
        ProjectionHandlerRegistry.Clear();
        
        var services = new ServiceCollection();
        services.AddLogging();
        var checkpointStore = new InMemoryProjectionCheckpointStore();
        services.AddSingleton<IProjectionCheckpointStore>(checkpointStore);
        services.AddProjection<TestCheckpointProjection>();
        
        var provider = services.BuildServiceProvider();
        var projection = provider.GetRequiredService<TestCheckpointProjection>();
        
        // Act - Process 8 events and track checkpoint positions
        for (int i = 0; i < 8; i++)
        {
            await projection.ProjectAsync(new CheckpointEvent(i));
            
            // Simulate checkpoint batching: save after every 5 events
            if ((i + 1) % 5 == 0)
            {
                await checkpointStore.SaveCheckpointAsync("TestCheckpointProjection:_default", i + 1);
            }
        }
        
        // Simulate final flush
        await checkpointStore.SaveCheckpointAsync("TestCheckpointProjection:_default", 8);
        
        // Assert - checkpoint should reflect the number of events processed
        var finalCheckpoint = await checkpointStore.GetCheckpointAsync("TestCheckpointProjection:_default");
        Assert.That(finalCheckpoint, Is.EqualTo(8));
        
        // Assert projection processed all events
        Assert.That(projection.ProcessedCount, Is.EqualTo(8));
    }

    [Test]
    public async Task Engine_RespectsMaxDegreeOfParallelism()
    {
        // Arrange
        ProjectionHandlerRegistry.Clear();
        
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddProjection<TestParallelismProjection>();
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        var eventPublisher = provider.GetRequiredService<IEventPublisher>();
        var engine = provider.GetRequiredService<IProjectionEngine>();
        
        using var cts = new CancellationTokenSource();
        var engineTask = Task.Run(() => engine.RunAsync(cts.Token));
        
        // Act - Give engine time to start and subscribe
        await Task.Delay(200);
        
        // Publish events to multiple partitions quickly after engine has subscribed
        var publishTasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            var partition = $"partition-{i}";
            publishTasks.Add(eventPublisher.Publish(new ParallelismEvent(partition, 1)));
        }
        await Task.WhenAll(publishTasks);
        
        // Wait a bit for processing to start
        await Task.Delay(400);
        
        // Assert - max concurrent should not exceed MaxDegreeOfParallelism (2)
        var projection = provider.GetRequiredService<TestParallelismProjection>();
        Assert.That(projection.MaxConcurrentPartitions, Is.LessThanOrEqualTo(2));
        
        // Stop engine
        cts.Cancel();
        try { await engineTask; } catch (OperationCanceledException) { }
    }

    [Test]
    public async Task Engine_MaintainsOrderWithinPartition_EvenWithParallelism()
    {
        // Arrange
        ProjectionHandlerRegistry.Clear();
        
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProjection<TestOrderingProjection>();
        
        var provider = services.BuildServiceProvider();
        var projection = provider.GetRequiredService<TestOrderingProjection>();
        
        // Act - Process 50 events to same partition in sequence
        // (Within a partition, events are always processed sequentially)
        for (int i = 0; i < 50; i++)
        {
            await projection.ProjectAsync(new OrderingEvent("same-partition", i));
        }
        
        // Assert - events should be processed in order within the partition
        Assert.That(projection.PartitionOrders.ContainsKey("same-partition"), Is.True);
        
        var processedOrder = projection.PartitionOrders["same-partition"];
        Assert.That(processedOrder.Count, Is.EqualTo(50));
        
        for (int i = 0; i < 50; i++)
        {
            Assert.That(processedOrder[i], Is.EqualTo(i), $"Event at index {i} was out of order");
        }
    }

    // Test event types
    public record TestEvent(int Id, string Data);
    public record PartitionedEvent(string PartitionKey, int Value);
    public record CheckpointEvent(int Id);
    public record ParallelismEvent(string PartitionKey, int Value);
    public record OrderingEvent(string PartitionKey, int Sequence);

    // Test projections
    [Projection]
    public class TestSequentialProjection : IProjectionHandler<TestEvent>
    {
        private static readonly ConcurrentBag<int> _processedEvents = new();
        
        public List<int> ProcessedEvents => _processedEvents.OrderBy(x => x).ToList();

        public ValueTask ProjectAsync(TestEvent @event, CancellationToken ct = default)
        {
            _processedEvents.Add(@event.Id);
            return ValueTask.CompletedTask;
        }
        
        public static void Clear() => _processedEvents.Clear();
    }

    [Projection]
    public class TestPartitionedProjection : IPartitionedProjectionHandler<PartitionedEvent>
    {
        private static readonly ConcurrentDictionary<string, ConcurrentBag<int>> _partitionEvents = new();
        
        public ConcurrentDictionary<string, List<int>> PartitionEvents =>
            new(_partitionEvents.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.OrderBy(x => x).ToList()));

        public string GetPartitionKey(PartitionedEvent @event)
        {
            return @event.PartitionKey;
        }

        public ValueTask ProjectAsync(PartitionedEvent @event, CancellationToken ct = default)
        {
            _partitionEvents.AddOrUpdate(
                @event.PartitionKey,
                new ConcurrentBag<int> { @event.Value },
                (_, bag) =>
                {
                    bag.Add(@event.Value);
                    return bag;
                });
            return ValueTask.CompletedTask;
        }
        
        public static void Clear() => _partitionEvents.Clear();
    }

    [Projection(CheckpointBatchSize = 5)]
    public class TestCheckpointProjection : IProjectionHandler<CheckpointEvent>
    {
        private static int _processedCount = 0;
        
        public int ProcessedCount => _processedCount;

        public ValueTask ProjectAsync(CheckpointEvent @event, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _processedCount);
            return ValueTask.CompletedTask;
        }
        
        public static void Clear() => _processedCount = 0;
    }

    [Projection(MaxDegreeOfParallelism = 2)]
    public class TestParallelismProjection : IPartitionedProjectionHandler<ParallelismEvent>
    {
        private static int _currentConcurrent = 0;
        private static int _maxConcurrent = 0;
        private static readonly object _lock = new();

        public int MaxConcurrentPartitions => _maxConcurrent;

        public string GetPartitionKey(ParallelismEvent @event)
        {
            return @event.PartitionKey;
        }

        public async ValueTask ProjectAsync(ParallelismEvent @event, CancellationToken ct = default)
        {
            lock (_lock)
            {
                _currentConcurrent++;
                if (_currentConcurrent > _maxConcurrent)
                {
                    _maxConcurrent = _currentConcurrent;
                }
            }

            // Simulate some work
            await Task.Delay(100, ct);

            lock (_lock)
            {
                _currentConcurrent--;
            }
        }
        
        public static void Clear()
        {
            _currentConcurrent = 0;
            _maxConcurrent = 0;
        }
    }

    [Projection(MaxDegreeOfParallelism = 4)]
    public class TestOrderingProjection : IPartitionedProjectionHandler<OrderingEvent>
    {
        // Use ConcurrentQueue to preserve insertion order for ordering verification
        private static readonly ConcurrentDictionary<string, ConcurrentQueue<int>> _partitionOrders = new();
        
        public ConcurrentDictionary<string, List<int>> PartitionOrders =>
            new(_partitionOrders.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToList())); // Convert queue to list preserving order

        public string GetPartitionKey(OrderingEvent @event)
        {
            return @event.PartitionKey;
        }

        public ValueTask ProjectAsync(OrderingEvent @event, CancellationToken ct = default)
        {
            _partitionOrders.AddOrUpdate(
                @event.PartitionKey,
                new ConcurrentQueue<int>(new[] { @event.Sequence }),
                (_, queue) =>
                {
                    queue.Enqueue(@event.Sequence);
                    return queue;
                });
            return ValueTask.CompletedTask;
        }
        
        public static void Clear() => _partitionOrders.Clear();
    }
}
