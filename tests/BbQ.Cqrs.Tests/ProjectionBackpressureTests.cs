using BbQ.Events;
using BbQ.Events.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.Collections.Concurrent;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Tests for projection backpressure and flow control functionality.
/// </summary>
[TestFixture]
public class ProjectionBackpressureTests
{
    [TearDown]
    public void TearDown()
    {
        // Clear registry between tests
        ProjectionHandlerRegistry.Clear();
        
        // Clear static test data
        SlowProjection.Clear();
        FastIngestionProjection.Clear();
    }

    [Test]
    public void ProjectionOptions_DefaultsToBlockStrategy()
    {
        // Arrange
        var options = new ProjectionOptions();
        
        // Assert
        Assert.That(options.BackpressureStrategy, Is.EqualTo(BackpressureStrategy.Block));
        Assert.That(options.ChannelCapacity, Is.EqualTo(1000));
    }

    [Test]
    public void BackpressureStrategy_HasExpectedValues()
    {
        // Assert - Verify enum values
        Assert.That((int)BackpressureStrategy.Block, Is.EqualTo(0));
        Assert.That((int)BackpressureStrategy.DropNewest, Is.EqualTo(1));
        Assert.That((int)BackpressureStrategy.DropOldest, Is.EqualTo(2));
    }

    [Test]
    public void ProjectionMetrics_TracksQueueDepth()
    {
        // Arrange
        var metrics = new ProjectionMetrics
        {
            ProjectionName = "TestProjection",
            PartitionKey = "_default"
        };
        
        // Act
        metrics.QueueDepth = 50;
        
        // Assert
        Assert.That(metrics.QueueDepth, Is.EqualTo(50));
    }

    [Test]
    public void ProjectionMetrics_TracksEventsDropped()
    {
        // Arrange
        var metrics = new ProjectionMetrics
        {
            ProjectionName = "TestProjection",
            PartitionKey = "_default"
        };
        
        // Act - Use setter directly since IncrementEventsDropped is internal
        metrics.EventsDropped = 3;
        
        // Assert
        Assert.That(metrics.EventsDropped, Is.EqualTo(3));
    }

    [Test]
    public void InMemoryProjectionMonitor_RecordsQueueDepth()
    {
        // Arrange
        var monitor = new InMemoryProjectionMonitor();
        
        // Act
        monitor.RecordQueueDepth("TestProjection", "partition-1", 42);
        monitor.RecordQueueDepth("TestProjection", "partition-1", 35);
        
        // Assert
        var metrics = monitor.GetMetrics("TestProjection", "partition-1");
        Assert.That(metrics, Is.Not.Null);
        Assert.That(metrics!.QueueDepth, Is.EqualTo(35), "Should track most recent queue depth");
    }

    [Test]
    public void InMemoryProjectionMonitor_RecordsEventDropped()
    {
        // Arrange
        var monitor = new InMemoryProjectionMonitor();
        
        // Act
        monitor.RecordEventDropped("TestProjection", "partition-1");
        monitor.RecordEventDropped("TestProjection", "partition-1");
        monitor.RecordEventDropped("TestProjection", "partition-1");
        
        // Assert
        var metrics = monitor.GetMetrics("TestProjection", "partition-1");
        Assert.That(metrics, Is.Not.Null);
        Assert.That(metrics!.EventsDropped, Is.EqualTo(3));
    }

    [Test]
    public void ProjectionAttribute_SupportsBackpressureConfiguration()
    {
        // Arrange
        var attribute = new ProjectionAttribute
        {
            ChannelCapacity = 500,
            BackpressureStrategy = BackpressureStrategy.DropOldest
        };
        
        // Assert
        Assert.That(attribute.ChannelCapacity, Is.EqualTo(500));
        Assert.That(attribute.BackpressureStrategy, Is.EqualTo(BackpressureStrategy.DropOldest));
    }

    [Test]
    public void ProjectionOptions_CanConfigureBackpressure()
    {
        // Arrange & Act
        var options = new ProjectionOptions
        {
            ChannelCapacity = 250,
            BackpressureStrategy = BackpressureStrategy.DropNewest
        };
        
        // Assert
        Assert.That(options.ChannelCapacity, Is.EqualTo(250));
        Assert.That(options.BackpressureStrategy, Is.EqualTo(BackpressureStrategy.DropNewest));
    }

    [Test]
    public void ProjectionRegistration_CanConfigureBackpressureViaAction()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        
        // Act
        services.AddProjection<SlowProjection>(options =>
        {
            options.ChannelCapacity = 100;
            options.BackpressureStrategy = BackpressureStrategy.Block;
        });
        
        // Assert - verify projection is registered
        var provider = services.BuildServiceProvider();
        var projection = provider.GetService<SlowProjection>();
        Assert.That(projection, Is.Not.Null);
    }
}

// Test event and projection for backpressure tests
public record SlowEvent(int Id);

[Projection]
public class SlowProjection : IProjectionHandler<SlowEvent>
{
    private static int _processedCount;
    
    public static int ProcessedCount => _processedCount;
    
    public static void Clear()
    {
        _processedCount = 0;
    }
    
    public async ValueTask ProjectAsync(SlowEvent evt, CancellationToken ct = default)
    {
        // Simulate slow processing (100ms per event = ~10 events/sec)
        await Task.Delay(100, ct);
        Interlocked.Increment(ref _processedCount);
    }
}

[Projection]
public class FastIngestionProjection : IProjectionHandler<SlowEvent>
{
    private static int _processedCount;
    
    public static int ProcessedCount => _processedCount;
    
    public static void Clear()
    {
        _processedCount = 0;
    }
    
    public async ValueTask ProjectAsync(SlowEvent evt, CancellationToken ct = default)
    {
        // Very fast processing
        await Task.Yield();
        Interlocked.Increment(ref _processedCount);
    }
}
