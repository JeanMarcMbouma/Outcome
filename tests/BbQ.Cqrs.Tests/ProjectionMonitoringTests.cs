using BbQ.Events;
using BbQ.Events.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Tests for projection monitoring and metrics functionality.
/// </summary>
[TestFixture]
public class ProjectionMonitoringTests
{
    [TearDown]
    public void TearDown()
    {
        // Clear registry between tests
        ProjectionHandlerRegistry.Clear();
        
        // Clear static test data
        TestMonitoredProjection.Clear();
    }

    [Test]
    public void InMemoryProjectionMonitor_RecordsEventProcessed()
    {
        // Arrange
        var monitor = new InMemoryProjectionMonitor();
        
        // Act
        monitor.RecordEventProcessed("TestProjection", "partition-1", 10);
        monitor.RecordEventProcessed("TestProjection", "partition-1", 11);
        monitor.RecordEventProcessed("TestProjection", "partition-1", 12);
        
        // Assert
        var metrics = monitor.GetMetrics("TestProjection", "partition-1");
        Assert.That(metrics, Is.Not.Null);
        Assert.That(metrics!.ProjectionName, Is.EqualTo("TestProjection"));
        Assert.That(metrics.PartitionKey, Is.EqualTo("partition-1"));
        Assert.That(metrics.CurrentPosition, Is.EqualTo(12));
        Assert.That(metrics.EventsProcessed, Is.EqualTo(3));
        Assert.That(metrics.LastEventProcessedTime, Is.Not.Null);
    }

    [Test]
    public void InMemoryProjectionMonitor_RecordsCheckpointWritten()
    {
        // Arrange
        var monitor = new InMemoryProjectionMonitor();
        
        // Act
        monitor.RecordCheckpointWritten("TestProjection", "partition-1", 100);
        monitor.RecordCheckpointWritten("TestProjection", "partition-1", 200);
        
        // Assert
        var metrics = monitor.GetMetrics("TestProjection", "partition-1");
        Assert.That(metrics, Is.Not.Null);
        Assert.That(metrics!.CheckpointsWritten, Is.EqualTo(2));
        Assert.That(metrics.LastCheckpointTime, Is.Not.Null);
    }

    [Test]
    public void InMemoryProjectionMonitor_RecordsLag()
    {
        // Arrange
        var monitor = new InMemoryProjectionMonitor();
        
        // Act
        monitor.RecordLag("TestProjection", "partition-1", 50, 100);
        
        // Assert
        var metrics = monitor.GetMetrics("TestProjection", "partition-1");
        Assert.That(metrics, Is.Not.Null);
        Assert.That(metrics!.CurrentPosition, Is.EqualTo(50));
        Assert.That(metrics.LatestEventPosition, Is.EqualTo(100));
        Assert.That(metrics.Lag, Is.EqualTo(50));
    }

    [Test]
    public void InMemoryProjectionMonitor_RecordsWorkerCount()
    {
        // Arrange
        var monitor = new InMemoryProjectionMonitor();
        
        // Create some metrics for different partitions
        monitor.RecordEventProcessed("TestProjection", "partition-1", 10);
        monitor.RecordEventProcessed("TestProjection", "partition-2", 20);
        
        // Act
        monitor.RecordWorkerCount("TestProjection", 5);
        
        // Assert
        var metrics1 = monitor.GetMetrics("TestProjection", "partition-1");
        var metrics2 = monitor.GetMetrics("TestProjection", "partition-2");
        Assert.That(metrics1!.WorkerCount, Is.EqualTo(5));
        Assert.That(metrics2!.WorkerCount, Is.EqualTo(5));
    }

    [Test]
    public void InMemoryProjectionMonitor_GetAllMetrics_ReturnsAll()
    {
        // Arrange
        var monitor = new InMemoryProjectionMonitor();
        monitor.RecordEventProcessed("Projection1", "partition-1", 10);
        monitor.RecordEventProcessed("Projection1", "partition-2", 20);
        monitor.RecordEventProcessed("Projection2", "partition-1", 30);
        
        // Act
        var allMetrics = monitor.GetAllMetrics().ToList();
        
        // Assert
        Assert.That(allMetrics.Count, Is.EqualTo(3));
        Assert.That(allMetrics.Any(m => m.ProjectionName == "Projection1" && m.PartitionKey == "partition-1"), Is.True);
        Assert.That(allMetrics.Any(m => m.ProjectionName == "Projection1" && m.PartitionKey == "partition-2"), Is.True);
        Assert.That(allMetrics.Any(m => m.ProjectionName == "Projection2" && m.PartitionKey == "partition-1"), Is.True);
    }

    [Test]
    public void ProjectionMetrics_CalculatesLag_WithNullLatestPosition()
    {
        // Arrange
        var metrics = new ProjectionMetrics
        {
            CurrentPosition = 100,
            LatestEventPosition = null
        };
        
        // Assert
        Assert.That(metrics.Lag, Is.EqualTo(0));
    }

    [Test]
    public void ProjectionMetrics_CalculatesLag_WhenCaughtUp()
    {
        // Arrange
        var metrics = new ProjectionMetrics
        {
            CurrentPosition = 100,
            LatestEventPosition = 100
        };
        
        // Assert
        Assert.That(metrics.Lag, Is.EqualTo(0));
    }

    [Test]
    public void ProjectionMetrics_CalculatesLag_WhenBehind()
    {
        // Arrange
        var metrics = new ProjectionMetrics
        {
            CurrentPosition = 50,
            LatestEventPosition = 150
        };
        
        // Assert
        Assert.That(metrics.Lag, Is.EqualTo(100));
    }

    [Test]
    public void ProjectionEngine_RegistersMonitor_ViaDependencyInjection()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        
        // Act
        var monitor = provider.GetService<IProjectionMonitor>();
        
        // Assert
        Assert.That(monitor, Is.Not.Null);
        Assert.That(monitor, Is.InstanceOf<InMemoryProjectionMonitor>());
    }

    [Test]
    public void Monitor_Integration_WithProjectionEngine()
    {
        // Arrange
        ProjectionHandlerRegistry.Clear();
        
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddProjection<TestMonitoredProjection>(options =>
        {
            options.CheckpointBatchSize = 2; // Checkpoint every 2 events
        });
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IProjectionMonitor>();
        var engine = provider.GetRequiredService<IProjectionEngine>();
        
        // Assert - Verify that monitor is properly registered and can be resolved
        Assert.That(monitor, Is.Not.Null, "Monitor should be registered via AddProjectionEngine");
        Assert.That(monitor, Is.InstanceOf<InMemoryProjectionMonitor>(), "Should use InMemoryProjectionMonitor by default");
        
        // Verify we can manually record metrics
        monitor.RecordEventProcessed("TestProjection", "_default", 1);
        monitor.RecordEventProcessed("TestProjection", "_default", 2);
        monitor.RecordCheckpointWritten("TestProjection", "_default", 2);
        
        var metrics = monitor.GetMetrics("TestProjection", "_default");
        Assert.That(metrics, Is.Not.Null);
        Assert.That(metrics!.EventsProcessed, Is.EqualTo(2));
        Assert.That(metrics.CheckpointsWritten, Is.EqualTo(1));
    }

    // Test projection for monitoring tests
    public class TestMonitoredProjection : IProjectionHandler<MonitoredEvent>
    {
        private static readonly List<int> _processedEventIds = new();

        public ValueTask ProjectAsync(MonitoredEvent @event, CancellationToken ct = default)
        {
            _processedEventIds.Add(@event.Id);
            return ValueTask.CompletedTask;
        }

        public static void Clear() => _processedEventIds.Clear();
        public static List<int> ProcessedEventIds => _processedEventIds;
    }

    public record MonitoredEvent(int Id, string Data);
}
