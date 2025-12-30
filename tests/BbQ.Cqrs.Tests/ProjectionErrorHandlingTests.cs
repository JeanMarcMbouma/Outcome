using BbQ.Events;
using BbQ.Events.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.Collections.Concurrent;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Tests for projection error handling and retry policies.
/// These tests verify the error handling behavior at the projection engine level.
/// </summary>
[TestFixture]
public class ProjectionErrorHandlingTests
{
    [TearDown]
    public void TearDown()
    {
        // Clear registry between tests
        ProjectionHandlerRegistry.Clear();
        
        // Clear static test data
        TestRetryProjection.Clear();
        TestSkipProjection.Clear();
        TestStopProjection.Clear();
    }

    [Test]
    public async Task Retry_SucceedsAfterTransientFailure()
    {
        // Arrange
        ProjectionHandlerRegistry.Clear();
        
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddInMemoryEventBus();
        services.AddProjection<TestRetryProjection>(options =>
        {
            options.ErrorHandling.Strategy = ProjectionErrorHandlingStrategy.Retry;
            options.ErrorHandling.MaxRetryAttempts = 3;
            options.ErrorHandling.InitialRetryDelayMs = 20; // Fast for tests
            options.ErrorHandling.MaxRetryDelayMs = 100;
            options.CheckpointBatchSize = 10;
        });
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        var eventPublisher = provider.GetRequiredService<IEventPublisher>();
        var engine = provider.GetRequiredService<IProjectionEngine>();
        
        // Configure projection to fail first 2 attempts, then succeed
        TestRetryProjection.FailureCount = 2;
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var engineTask = Task.Run(async () =>
        {
            try { await engine.RunAsync(cts.Token); }
            catch (OperationCanceledException) { }
        });
        
        // Wait for engine to start
        await Task.Delay(300);
        
        // Act - Publish an event that will fail twice, then succeed
        await eventPublisher.Publish(new RetryEvent(1, "test"));
        
        // Wait for processing with retries
        await Task.Delay(500);
        
        // Stop engine
        cts.Cancel();
        await engineTask;
        
        // Assert - Event should be processed successfully after retries
        Assert.That(TestRetryProjection.ProcessedEvents.Count, Is.EqualTo(1), "Event should be processed");
        Assert.That(TestRetryProjection.AttemptCounts.ContainsKey(1), Is.True, "Attempt count should be tracked");
        Assert.That(TestRetryProjection.AttemptCounts[1], Is.EqualTo(3), "Should have 3 attempts: 2 failures + 1 success");
    }

    [Test]
    public async Task Retry_FallbackToSkip_ContinuesAfterExhaustedRetries()
    {
        // Arrange
        ProjectionHandlerRegistry.Clear();
        
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddInMemoryEventBus();
        services.AddProjection<TestRetryProjection>(options =>
        {
            options.ErrorHandling.Strategy = ProjectionErrorHandlingStrategy.Retry;
            options.ErrorHandling.MaxRetryAttempts = 2;
            options.ErrorHandling.InitialRetryDelayMs = 20;
            options.ErrorHandling.MaxRetryDelayMs = 50;
            options.ErrorHandling.FallbackStrategy = ProjectionErrorHandlingStrategy.Skip;
        });
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        var eventPublisher = provider.GetRequiredService<IEventPublisher>();
        var engine = provider.GetRequiredService<IProjectionEngine>();
        
        // Configure to always fail
        TestRetryProjection.FailureCount = 999;
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var engineTask = Task.Run(async () =>
        {
            try { await engine.RunAsync(cts.Token); }
            catch (OperationCanceledException) { }
        });
        
        await Task.Delay(300);
        
        // Act - Publish events: one will fail, next should still be processed
        await eventPublisher.Publish(new RetryEvent(1, "fail"));
        await Task.Delay(400); // Allow retries
        
        // Reset failure so second event succeeds
        TestRetryProjection.FailureCount = 0;
        await eventPublisher.Publish(new RetryEvent(2, "success"));
        await Task.Delay(200);
        
        cts.Cancel();
        await engineTask;
        
        // Assert - First event should fail and be skipped, second should succeed
        Assert.That(TestRetryProjection.ProcessedEvents.Count, Is.EqualTo(1), "Second event should be processed");
        var processedList = TestRetryProjection.ProcessedEvents.ToList();
        Assert.That(processedList[0], Is.EqualTo(2), "Only event 2 should succeed");
    }

    [Test]
    public async Task Skip_ContinuesProcessingAfterError()
    {
        // Arrange
        ProjectionHandlerRegistry.Clear();
        
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddInMemoryEventBus();
        services.AddProjection<TestSkipProjection>(options =>
        {
            options.ErrorHandling.Strategy = ProjectionErrorHandlingStrategy.Skip;
        });
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        var eventPublisher = provider.GetRequiredService<IEventPublisher>();
        var engine = provider.GetRequiredService<IProjectionEngine>();
        
        // Configure to fail on odd IDs
        TestSkipProjection.FailOnOddIds = true;
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var engineTask = Task.Run(async () =>
        {
            try { await engine.RunAsync(cts.Token); }
            catch (OperationCanceledException) { }
        });
        
        await Task.Delay(300);
        
        // Act - Publish mix of events that succeed and fail
        await eventPublisher.Publish(new SkipEvent(1)); // Fails
        await Task.Delay(100);
        await eventPublisher.Publish(new SkipEvent(2)); // Succeeds
        await Task.Delay(100);
        await eventPublisher.Publish(new SkipEvent(3)); // Fails
        await Task.Delay(100);
        await eventPublisher.Publish(new SkipEvent(4)); // Succeeds
        await Task.Delay(200);
        
        cts.Cancel();
        await engineTask;
        
        // Assert - Only even IDs should be processed
        Assert.That(TestSkipProjection.ProcessedEvents.Count, Is.EqualTo(2), "Only non-failing events should be processed");
        var processedList = TestSkipProjection.ProcessedEvents.OrderBy(x => x).ToList();
        CollectionAssert.AreEqual(new[] { 2, 4 }, processedList, "Events 2 and 4 should be processed");
    }

    [Test]
    public async Task Stop_HaltsWorkerOnError()
    {
        // Arrange
        ProjectionHandlerRegistry.Clear();
        
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddInMemoryEventBus();
        services.AddProjection<TestStopProjection>(options =>
        {
            options.ErrorHandling.Strategy = ProjectionErrorHandlingStrategy.Stop;
        });
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        var eventPublisher = provider.GetRequiredService<IEventPublisher>();
        var engine = provider.GetRequiredService<IProjectionEngine>();
        
        // Configure to fail on first event
        TestStopProjection.FailOnId = 1;
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var engineTask = Task.Run(async () =>
        {
            try { await engine.RunAsync(cts.Token); }
            catch (OperationCanceledException) { }
        });
        
        await Task.Delay(300);
        
        // Act
        await eventPublisher.Publish(new StopEvent(1)); // Should fail and stop
        await Task.Delay(200);
        await eventPublisher.Publish(new StopEvent(2)); // Should not be processed
        await Task.Delay(200);
        
        cts.Cancel();
        await engineTask;
        
        // Assert - Worker should stop after first failure
        Assert.That(TestStopProjection.ProcessedEvents.Count, Is.EqualTo(0), "No events should be processed");
        Assert.That(TestStopProjection.FailedEventId, Is.EqualTo(1), "Event 1 should have failed");
    }

    // Test Event Types
    public record RetryEvent(int Id, string Data);
    public record SkipEvent(int Id);
    public record StopEvent(int Id);

    // Test Projection: Retry Strategy
    [Projection]
    public class TestRetryProjection : IProjectionHandler<RetryEvent>
    {
        public static int FailureCount = 0;
        public static ConcurrentBag<int> ProcessedEvents = new();
        public static ConcurrentDictionary<int, int> AttemptCounts = new();

        public static void Clear()
        {
            FailureCount = 0;
            ProcessedEvents.Clear();
            AttemptCounts.Clear();
        }

        public ValueTask ProjectAsync(RetryEvent evt, CancellationToken ct = default)
        {
            AttemptCounts.AddOrUpdate(evt.Id, 1, (_, count) => count + 1);
            
            if (FailureCount > 0)
            {
                FailureCount--;
                throw new InvalidOperationException($"Transient failure for event {evt.Id}");
            }
            
            ProcessedEvents.Add(evt.Id);
            return ValueTask.CompletedTask;
        }
    }

    // Test Projection: Skip Strategy
    [Projection]
    public class TestSkipProjection : IProjectionHandler<SkipEvent>
    {
        public static bool FailOnOddIds = false;
        public static ConcurrentBag<int> ProcessedEvents = new();

        public static void Clear()
        {
            FailOnOddIds = false;
            ProcessedEvents.Clear();
        }

        public ValueTask ProjectAsync(SkipEvent evt, CancellationToken ct = default)
        {
            if (FailOnOddIds && evt.Id % 2 == 1)
            {
                throw new InvalidOperationException($"Configured to fail on odd ID: {evt.Id}");
            }
            
            ProcessedEvents.Add(evt.Id);
            return ValueTask.CompletedTask;
        }
    }

    // Test Projection: Stop Strategy
    [Projection]
    public class TestStopProjection : IProjectionHandler<StopEvent>
    {
        public static int FailOnId = -1;
        public static int FailedEventId = -1;
        public static ConcurrentBag<int> ProcessedEvents = new();

        public static void Clear()
        {
            FailOnId = -1;
            FailedEventId = -1;
            ProcessedEvents.Clear();
        }

        public ValueTask ProjectAsync(StopEvent evt, CancellationToken ct = default)
        {
            if (evt.Id == FailOnId)
            {
                FailedEventId = evt.Id;
                throw new InvalidOperationException($"Configured to fail on ID: {evt.Id}");
            }
            
            ProcessedEvents.Add(evt.Id);
            return ValueTask.CompletedTask;
        }
    }
}
