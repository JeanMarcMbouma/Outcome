using BbQ.Events;
using NUnit.Framework;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Tests for projection error handling configuration and options.
/// </summary>
[TestFixture]
public class ProjectionErrorHandlingOptionsTests
{
    [Test]
    public void ProjectionErrorHandlingOptions_HasCorrectDefaults()
    {
        // Arrange & Act
        var options = new ProjectionErrorHandlingOptions();
        
        // Assert - Verify default values match specification
        Assert.That(options.Strategy, Is.EqualTo(ProjectionErrorHandlingStrategy.Retry), 
            "Default strategy should be Retry");
        Assert.That(options.MaxRetryAttempts, Is.EqualTo(3), 
            "Default max retry attempts should be 3");
        Assert.That(options.InitialRetryDelayMs, Is.EqualTo(1000), 
            "Default initial retry delay should be 1000ms");
        Assert.That(options.MaxRetryDelayMs, Is.EqualTo(30000), 
            "Default max retry delay should be 30000ms");
        Assert.That(options.FallbackStrategy, Is.EqualTo(ProjectionErrorHandlingStrategy.Skip), 
            "Default fallback strategy should be Skip");
    }

    [Test]
    public void ProjectionOptions_IncludesErrorHandlingOptions()
    {
        // Arrange & Act
        var options = new ProjectionOptions();
        
        // Assert - Verify error handling is included and initialized
        Assert.That(options.ErrorHandling, Is.Not.Null, 
            "ErrorHandling should be initialized");
        Assert.That(options.ErrorHandling.Strategy, Is.EqualTo(ProjectionErrorHandlingStrategy.Retry), 
            "Default error handling strategy should be Retry");
    }

    [Test]
    public void ProjectionErrorHandlingOptions_CanBeConfigured()
    {
        // Arrange
        var options = new ProjectionErrorHandlingOptions
        {
            Strategy = ProjectionErrorHandlingStrategy.Skip,
            MaxRetryAttempts = 5,
            InitialRetryDelayMs = 500,
            MaxRetryDelayMs = 10000,
            FallbackStrategy = ProjectionErrorHandlingStrategy.Stop
        };
        
        // Act & Assert - Verify all properties can be set
        Assert.That(options.Strategy, Is.EqualTo(ProjectionErrorHandlingStrategy.Skip));
        Assert.That(options.MaxRetryAttempts, Is.EqualTo(5));
        Assert.That(options.InitialRetryDelayMs, Is.EqualTo(500));
        Assert.That(options.MaxRetryDelayMs, Is.EqualTo(10000));
        Assert.That(options.FallbackStrategy, Is.EqualTo(ProjectionErrorHandlingStrategy.Stop));
    }

    [Test]
    public void ProjectionOptions_ErrorHandlingCanBeConfigured()
    {
        // Arrange
        var options = new ProjectionOptions
        {
            ProjectionName = "TestProjection",
            ErrorHandling = new ProjectionErrorHandlingOptions
            {
                Strategy = ProjectionErrorHandlingStrategy.Stop,
                MaxRetryAttempts = 2
            }
        };
        
        // Act & Assert
        Assert.That(options.ErrorHandling.Strategy, Is.EqualTo(ProjectionErrorHandlingStrategy.Stop));
        Assert.That(options.ErrorHandling.MaxRetryAttempts, Is.EqualTo(2));
    }

    [Test]
    public void ProjectionErrorHandlingStrategy_HasThreeStrategies()
    {
        // Arrange & Act - Verify all strategies are available
        var strategies = Enum.GetValues<ProjectionErrorHandlingStrategy>();
        
        // Assert
        Assert.That(strategies.Length, Is.EqualTo(3), "Should have exactly 3 strategies");
        Assert.That(strategies, Contains.Item(ProjectionErrorHandlingStrategy.Retry));
        Assert.That(strategies, Contains.Item(ProjectionErrorHandlingStrategy.Skip));
        Assert.That(strategies, Contains.Item(ProjectionErrorHandlingStrategy.Stop));
    }
}
