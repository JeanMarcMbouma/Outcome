using BbQ.Events.Checkpointing;
using BbQ.Events.Configuration;
using BbQ.Events.Engine;
using BbQ.Events.Events;
using BbQ.Events.Projections;

using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Tests for source generator projection registration.
/// </summary>
[TestFixture]
public class SourceGeneratorProjectionTests
{
    [Test]
    public void AddProjectionsFromAssembly_RegistersProjections_WhenAttributePresent()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();

        // Act - Use AddProjectionsFromAssembly which scans for [Projection] attributes
        // This tests the runtime discovery mechanism
        services.AddProjectionsFromAssembly(typeof(ProjectionTests).Assembly);
        var provider = services.BuildServiceProvider();

        // Assert - Check that projections are registered
        var profileProjection = provider.GetService<ProjectionTests.TestUserProfileProjection>();
        var statsProjection = provider.GetService<ProjectionTests.TestUserStatisticsProjection>();
        
        Assert.That(profileProjection, Is.Not.Null, "UserProfileProjection should be registered");
        Assert.That(statsProjection, Is.Not.Null, "UserStatisticsProjection should be registered");

        // Verify handlers are also registered
        var createdHandler = provider.GetService<IProjectionHandler<ProjectionTests.UserCreatedEvent>>();
        var activityHandler = provider.GetService<IPartitionedProjectionHandler<ProjectionTests.UserActivityEvent>>();
        
        Assert.That(createdHandler, Is.Not.Null, "IProjectionHandler should be registered");
        Assert.That(activityHandler, Is.Not.Null, "IPartitionedProjectionHandler should be registered");
    }
}
