using BbQ.Events;
using BbQ.Events.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Tests for projection startup mode functionality.
/// </summary>
[TestFixture]
public class ProjectionStartupModeTests
{
    [SetUp]
    public void Setup()
    {
        ProjectionHandlerRegistry.Clear();
    }

    [Test]
    public void ProjectionOptions_DefaultStartupMode_IsResume()
    {
        // Arrange & Act
        var options = new ProjectionOptions();

        // Assert
        Assert.That(options.StartupMode, Is.EqualTo(ProjectionStartupMode.Resume));
    }

    [Test]
    public void AddProjection_WithStartupModeConfiguration_SetsMode()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();

        // Act
        services.AddProjection<TestUserProfileProjection>(options =>
        {
            options.StartupMode = ProjectionStartupMode.Replay;
        });

        var provider = services.BuildServiceProvider();

        // Assert
        var registeredOptions = ProjectionHandlerRegistry.GetProjectionOptions(nameof(TestUserProfileProjection));
        Assert.That(registeredOptions, Is.Not.Null);
        Assert.That(registeredOptions.StartupMode, Is.EqualTo(ProjectionStartupMode.Replay));
    }

    [Test]
    public async Task CheckpointStore_ResetCheckpoint_ClearsExistingCheckpoint()
    {
        // Arrange
        var store = new InMemoryProjectionCheckpointStore();
        var projectionName = "TestProjection";
        
        // Save a checkpoint
        await store.SaveCheckpointAsync(projectionName, 100);
        var checkpointBeforeReset = await store.GetCheckpointAsync(projectionName);
        
        // Act
        await store.ResetCheckpointAsync(projectionName);
        var checkpointAfterReset = await store.GetCheckpointAsync(projectionName);

        // Assert
        Assert.That(checkpointBeforeReset, Is.EqualTo(100));
        Assert.That(checkpointAfterReset, Is.Null);
    }

    [Test]
    public void ProjectionStartupMode_AllModesAreDefined()
    {
        // Arrange & Act
        var modeValues = Enum.GetValues<ProjectionStartupMode>();

        // Assert
        Assert.That(modeValues, Contains.Item(ProjectionStartupMode.Resume));
        Assert.That(modeValues, Contains.Item(ProjectionStartupMode.Replay));
        Assert.That(modeValues, Contains.Item(ProjectionStartupMode.CatchUp));
        Assert.That(modeValues, Contains.Item(ProjectionStartupMode.LiveOnly));
    }

    [Test]
    [TestCase(ProjectionStartupMode.Resume)]
    [TestCase(ProjectionStartupMode.Replay)]
    [TestCase(ProjectionStartupMode.CatchUp)]
    [TestCase(ProjectionStartupMode.LiveOnly)]
    public void AddProjection_WithStartupMode_CanBeRegisteredAndConfigured(ProjectionStartupMode mode)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();

        // Act
        services.AddProjection<TestUserProfileProjection>(options =>
        {
            options.StartupMode = mode;
        });

        var provider = services.BuildServiceProvider();

        // Assert - projection can be resolved
        var projection = provider.GetService<TestUserProfileProjection>();
        Assert.That(projection, Is.Not.Null);
        
        // Assert - startup mode is correctly configured
        var registeredOptions = ProjectionHandlerRegistry.GetProjectionOptions(nameof(TestUserProfileProjection));
        Assert.That(registeredOptions, Is.Not.Null);
        Assert.That(registeredOptions.StartupMode, Is.EqualTo(mode));
    }

    [Test]
    public void AddProjection_MultipleProjectionsWithDifferentModes_CanBeRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();

        // Act
        services.AddProjection<TestUserProfileProjection>(options =>
        {
            options.StartupMode = ProjectionStartupMode.Resume;
        });
        
        services.AddProjection<TestUserStatisticsProjection>(options =>
        {
            options.StartupMode = ProjectionStartupMode.LiveOnly;
        });

        var provider = services.BuildServiceProvider();

        // Assert
        var profileProjection = provider.GetService<TestUserProfileProjection>();
        var statsProjection = provider.GetService<TestUserStatisticsProjection>();
        
        Assert.That(profileProjection, Is.Not.Null);
        Assert.That(statsProjection, Is.Not.Null);
        
        var profileOptions = ProjectionHandlerRegistry.GetProjectionOptions(nameof(TestUserProfileProjection));
        var statsOptions = ProjectionHandlerRegistry.GetProjectionOptions(nameof(TestUserStatisticsProjection));
        
        Assert.That(profileOptions?.StartupMode, Is.EqualTo(ProjectionStartupMode.Resume));
        Assert.That(statsOptions?.StartupMode, Is.EqualTo(ProjectionStartupMode.LiveOnly));
    }

    // Test event types (reusing from ProjectionTests)
    public record UserCreatedEvent(string UserId, string Name, string Email);
    public record UserUpdatedEvent(string UserId, string Name, string Email);
    public record UserActivityEvent(string UserId, string ActivityType);

    // Test projections
    [Projection]
    public class TestUserProfileProjection :
        IProjectionHandler<UserCreatedEvent>,
        IProjectionHandler<UserUpdatedEvent>
    {
        public ValueTask ProjectAsync(UserCreatedEvent @event, CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask ProjectAsync(UserUpdatedEvent @event, CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }
    }

    [Projection]
    public class TestUserStatisticsProjection : IPartitionedProjectionHandler<UserActivityEvent>
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
