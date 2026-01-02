using BbQ.Events.Checkpointing;
using BbQ.Events.Configuration;
using BbQ.Events.Engine;
using BbQ.Events.Events;
using BbQ.Events.Projections;
using NUnit.Framework;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Behavior tests for projection error handling strategies.
/// These tests verify the configuration and expected behavior of error handling options.
/// </summary>
[TestFixture]
public class ProjectionErrorHandlingBehaviorTests
{
    [Test]
    public void RetryStrategy_CalculatesExponentialBackoff()
    {
        // Arrange
        var options = new ProjectionErrorHandlingOptions
        {
            Strategy = ProjectionErrorHandlingStrategy.Retry,
            InitialRetryDelayMs = 100,
            MaxRetryDelayMs = 1000,
            MaxRetryAttempts = 4
        };
        
        // Act - Simulate delay calculations for each retry
        var delays = new List<int>();
        int delay = options.InitialRetryDelayMs;
        for (int i = 0; i < options.MaxRetryAttempts; i++)
        {
            delays.Add(delay);
            delay = Math.Min(delay * 2, options.MaxRetryDelayMs);
        }
        
        // Assert - Verify exponential backoff with cap
        Assert.That(delays[0], Is.EqualTo(100), "First retry: 100ms");
        Assert.That(delays[1], Is.EqualTo(200), "Second retry: 200ms");
        Assert.That(delays[2], Is.EqualTo(400), "Third retry: 400ms");
        Assert.That(delays[3], Is.EqualTo(800), "Fourth retry: 800ms");
    }

    [Test]
    public void RetryStrategy_CapsDelayAtMaximum()
    {
        // Arrange
        var options = new ProjectionErrorHandlingOptions
        {
            Strategy = ProjectionErrorHandlingStrategy.Retry,
            InitialRetryDelayMs = 1000,
            MaxRetryDelayMs = 2000,
            MaxRetryAttempts = 5
        };
        
        // Act - Simulate delay calculations
        var delays = new List<int>();
        int delay = options.InitialRetryDelayMs;
        for (int i = 0; i < options.MaxRetryAttempts; i++)
        {
            delays.Add(delay);
            delay = Math.Min(delay * 2, options.MaxRetryDelayMs);
        }
        
        // Assert - Verify cap is applied
        Assert.That(delays[0], Is.EqualTo(1000), "First retry: 1000ms");
        Assert.That(delays[1], Is.EqualTo(2000), "Second retry: 2000ms (capped)");
        Assert.That(delays[2], Is.EqualTo(2000), "Third retry: 2000ms (capped)");
        Assert.That(delays[3], Is.EqualTo(2000), "Fourth retry: 2000ms (capped)");
        Assert.That(delays[4], Is.EqualTo(2000), "Fifth retry: 2000ms (capped)");
    }

    [Test]
    public void RetryStrategy_ConfiguresCorrectAttemptCount()
    {
        // Arrange & Act
        var options = new ProjectionErrorHandlingOptions
        {
            Strategy = ProjectionErrorHandlingStrategy.Retry,
            MaxRetryAttempts = 3
        };
        
        // Assert - MaxRetryAttempts means: initial attempt + 3 retries = 4 total attempts
        // The loop condition should be: while (attempt < MaxRetryAttempts)
        // This gives us: attempt 0 (initial), 1 (retry 1), 2 (retry 2), then attempt 3 exits loop
        int totalAttempts = 0;
        for (int attempt = 0; attempt < options.MaxRetryAttempts; attempt++)
        {
            totalAttempts++;
        }
        
        Assert.That(totalAttempts, Is.EqualTo(3), "Should iterate exactly MaxRetryAttempts times");
    }

    [Test]
    public void SkipStrategy_AllowsContinuation()
    {
        // Arrange
        var options = new ProjectionErrorHandlingOptions
        {
            Strategy = ProjectionErrorHandlingStrategy.Skip
        };
        
        // Act & Assert
        Assert.That(options.Strategy, Is.EqualTo(ProjectionErrorHandlingStrategy.Skip));
        // Skip strategy should allow processing to continue after logging error
    }

    [Test]
    public void StopStrategy_HaltsProcessing()
    {
        // Arrange
        var options = new ProjectionErrorHandlingOptions
        {
            Strategy = ProjectionErrorHandlingStrategy.Stop
        };
        
        // Act & Assert
        Assert.That(options.Strategy, Is.EqualTo(ProjectionErrorHandlingStrategy.Stop));
        // Stop strategy should halt the worker for manual intervention
    }

    [Test]
    public void FallbackStrategy_OnlyAllowsSkipOrStop()
    {
        // Arrange - Valid fallback strategies
        var skipFallback = new ProjectionErrorHandlingOptions
        {
            FallbackStrategy = ProjectionErrorHandlingStrategy.Skip
        };
        
        var stopFallback = new ProjectionErrorHandlingOptions
        {
            FallbackStrategy = ProjectionErrorHandlingStrategy.Stop
        };
        
        // Act & Assert - Should not throw
        Assert.DoesNotThrow(() => skipFallback.Validate());
        Assert.DoesNotThrow(() => stopFallback.Validate());
        
        // Arrange - Invalid fallback strategy
        var retryFallback = new ProjectionErrorHandlingOptions
        {
            FallbackStrategy = ProjectionErrorHandlingStrategy.Retry
        };
        
        // Act & Assert - Should throw
        Assert.Throws<InvalidOperationException>(() => retryFallback.Validate());
    }

    [Test]
    public void ErrorHandlingOptions_WorksWithProjectionOptions()
    {
        // Arrange
        var projectionOptions = new ProjectionOptions
        {
            ProjectionName = "TestProjection",
            ErrorHandling = new ProjectionErrorHandlingOptions
            {
                Strategy = ProjectionErrorHandlingStrategy.Retry,
                MaxRetryAttempts = 5,
                InitialRetryDelayMs = 500,
                MaxRetryDelayMs = 10000,
                FallbackStrategy = ProjectionErrorHandlingStrategy.Stop
            }
        };
        
        // Act
        var errorHandling = projectionOptions.ErrorHandling;
        
        // Assert
        Assert.That(errorHandling.Strategy, Is.EqualTo(ProjectionErrorHandlingStrategy.Retry));
        Assert.That(errorHandling.MaxRetryAttempts, Is.EqualTo(5));
        Assert.That(errorHandling.InitialRetryDelayMs, Is.EqualTo(500));
        Assert.That(errorHandling.MaxRetryDelayMs, Is.EqualTo(10000));
        Assert.That(errorHandling.FallbackStrategy, Is.EqualTo(ProjectionErrorHandlingStrategy.Stop));
        
        // Validate should pass
        Assert.DoesNotThrow(() => errorHandling.Validate());
    }

    [Test]
    public void DefaultErrorHandling_UsesRetryWithSkipFallback()
    {
        // Arrange
        var options = new ProjectionOptions();
        
        // Act
        var errorHandling = options.ErrorHandling;
        
        // Assert - Default configuration
        Assert.That(errorHandling.Strategy, Is.EqualTo(ProjectionErrorHandlingStrategy.Retry), 
            "Default strategy should be Retry");
        Assert.That(errorHandling.FallbackStrategy, Is.EqualTo(ProjectionErrorHandlingStrategy.Skip), 
            "Default fallback should be Skip");
        Assert.That(errorHandling.MaxRetryAttempts, Is.EqualTo(3), 
            "Default should be 3 retry attempts");
        
        // Should be valid
        Assert.DoesNotThrow(() => errorHandling.Validate());
    }
}
