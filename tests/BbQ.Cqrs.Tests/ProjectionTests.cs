using BbQ.Events.Checkpointing;
using BbQ.Events.Configuration;
using BbQ.Events.Engine;
using BbQ.Events.Events;
using BbQ.Events.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.Collections.Concurrent;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Tests for projection functionality including handlers, engine, and registration.
/// </summary>
[TestFixture]
public class ProjectionTests
{
    private ServiceProvider _serviceProvider = null!;
    private IEventBus _eventBus = null!;
    private IEventPublisher _eventPublisher = null!;

    [SetUp]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        
        _serviceProvider = services.BuildServiceProvider();
        _eventBus = _serviceProvider.GetRequiredService<IEventBus>();
        _eventPublisher = _serviceProvider.GetRequiredService<IEventPublisher>();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider?.Dispose();
    }

    [Test]
    public void AddProjection_RegistersProjectionHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();

        // Act
        services.AddProjection<TestUserProfileProjection>();
        var provider = services.BuildServiceProvider();

        // Assert
        var projection = provider.GetService<TestUserProfileProjection>();
        Assert.That(projection, Is.Not.Null);

        var handler = provider.GetService<IProjectionHandler<UserCreatedEvent>>();
        Assert.That(handler, Is.Not.Null);
        Assert.That(handler, Is.InstanceOf<TestUserProfileProjection>());
    }

    [Test]
    public void AddProjection_WithMultipleEventTypes_RegistersAllHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();

        // Act
        services.AddProjection<TestUserProfileProjection>();
        var provider = services.BuildServiceProvider();

        // Assert
        var createdHandler = provider.GetService<IProjectionHandler<UserCreatedEvent>>();
        var updatedHandler = provider.GetService<IProjectionHandler<UserUpdatedEvent>>();
        
        Assert.That(createdHandler, Is.Not.Null);
        Assert.That(updatedHandler, Is.Not.Null);
        
        // Both should resolve to the same projection instance (when scoped)
        using var scope = provider.CreateScope();
        var h1 = scope.ServiceProvider.GetRequiredService<IProjectionHandler<UserCreatedEvent>>();
        var h2 = scope.ServiceProvider.GetRequiredService<IProjectionHandler<UserUpdatedEvent>>();
        Assert.That(h1, Is.SameAs(h2));
    }

    [Test]
    public void AddProjectionEngine_RegistersEngineAndCheckpointStore()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();

        // Act
        services.AddProjectionEngine();
        var provider = services.BuildServiceProvider();

        // Assert
        var engine = provider.GetService<IProjectionEngine>();
        var checkpointStore = provider.GetService<IProjectionCheckpointStore>();
        
        Assert.That(engine, Is.Not.Null);
        Assert.That(checkpointStore, Is.Not.Null);
        Assert.That(checkpointStore, Is.InstanceOf<InMemoryProjectionCheckpointStore>());
    }

    [Test]
    public async Task ProjectionHandler_ProjectsEventCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddProjection<TestUserProfileProjection>();
        
        var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IProjectionHandler<UserCreatedEvent>>();

        var evt = new UserCreatedEvent("user-1", "John Doe", "john@example.com");

        // Act
        await handler.ProjectAsync(evt);

        // Assert
        var projection = (TestUserProfileProjection)handler;
        Assert.That(projection.ProjectedUsers.ContainsKey("user-1"), Is.True);
        Assert.That(projection.ProjectedUsers["user-1"].Name, Is.EqualTo("John Doe"));
        Assert.That(projection.ProjectedUsers["user-1"].Email, Is.EqualTo("john@example.com"));
    }

    [Test]
    public async Task PartitionedProjectionHandler_GetPartitionKey_ReturnsCorrectKey()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddProjection<TestUserStatisticsProjection>();
        
        var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IPartitionedProjectionHandler<UserActivityEvent>>();

        var evt = new UserActivityEvent("user-1", "login");

        // Act
        var partitionKey = handler.GetPartitionKey(evt);

        // Assert
        Assert.That(partitionKey, Is.EqualTo("user-1"));
    }

    [Test]
    public async Task PartitionedProjectionHandler_ProjectsEventCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddProjection<TestUserStatisticsProjection>();
        
        var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IPartitionedProjectionHandler<UserActivityEvent>>();

        var evt1 = new UserActivityEvent("user-1", "login");
        var evt2 = new UserActivityEvent("user-1", "view-page");
        var evt3 = new UserActivityEvent("user-2", "login");

        // Act
        await handler.ProjectAsync(evt1);
        await handler.ProjectAsync(evt2);
        await handler.ProjectAsync(evt3);

        // Assert
        var projection = (TestUserStatisticsProjection)handler;
        Assert.That(projection.UserActivityCounts["user-1"], Is.EqualTo(2));
        Assert.That(projection.UserActivityCounts["user-2"], Is.EqualTo(1));
    }

    [Test]
    public async Task InMemoryCheckpointStore_SaveAndGetCheckpoint_WorksCorrectly()
    {
        // Arrange
        var store = new InMemoryProjectionCheckpointStore();
        var projectionName = "TestProjection";

        // Act & Assert - initially no checkpoint
        var initialCheckpoint = await store.GetCheckpointAsync(projectionName);
        Assert.That(initialCheckpoint, Is.Null);

        // Act - save checkpoint
        await store.SaveCheckpointAsync(projectionName, 100);
        
        // Assert - checkpoint saved
        var savedCheckpoint = await store.GetCheckpointAsync(projectionName);
        Assert.That(savedCheckpoint, Is.EqualTo(100));

        // Act - update checkpoint
        await store.SaveCheckpointAsync(projectionName, 200);
        
        // Assert - checkpoint updated
        var updatedCheckpoint = await store.GetCheckpointAsync(projectionName);
        Assert.That(updatedCheckpoint, Is.EqualTo(200));
    }

    [Test]
    public async Task InMemoryCheckpointStore_ResetCheckpoint_RemovesCheckpoint()
    {
        // Arrange
        var store = new InMemoryProjectionCheckpointStore();
        var projectionName = "TestProjection";
        await store.SaveCheckpointAsync(projectionName, 100);

        // Act
        await store.ResetCheckpointAsync(projectionName);

        // Assert
        var checkpoint = await store.GetCheckpointAsync(projectionName);
        Assert.That(checkpoint, Is.Null);
    }

    [Test]
    public void AddProjectionsFromAssembly_RegistersAllProjections()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();

        // Act
        services.AddProjectionsFromAssembly(typeof(ProjectionTests).Assembly);
        var provider = services.BuildServiceProvider();

        // Assert
        var userProfileProjection = provider.GetService<TestUserProfileProjection>();
        var userStatsProjection = provider.GetService<TestUserStatisticsProjection>();
        
        Assert.That(userProfileProjection, Is.Not.Null);
        Assert.That(userStatsProjection, Is.Not.Null);
    }

    [Test]
    public void ProjectionEngine_CanBeCreated_WithRegisteredProjections()
    {
        // Arrange
        ProjectionHandlerRegistry.Clear();
        
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddProjection<TestUserProfileProjection>();
        services.AddProjection<TestUserStatisticsProjection>();
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        
        // Act
        var engine = provider.GetService<IProjectionEngine>();
        
        // Assert
        Assert.That(engine, Is.Not.Null, "Projection engine should be registered");
        
        // Verify handlers are in registry
        var registeredEvents = ProjectionHandlerRegistry.GetEventTypes().ToList();
        Assert.That(registeredEvents.Count, Is.GreaterThan(0), "Should have registered event types");
        Assert.That(registeredEvents, Contains.Item(typeof(UserCreatedEvent)), "Should have UserCreatedEvent registered");
        Assert.That(registeredEvents, Contains.Item(typeof(UserActivityEvent)), "Should have UserActivityEvent registered");
    }

    // Test event types
    public record UserCreatedEvent(string UserId, string Name, string Email);
    public record UserUpdatedEvent(string UserId, string Name, string Email);
    public record UserActivityEvent(string UserId, string ActivityType);

    // Test projection: Multi-event projection
    [Projection]
    public class TestUserProfileProjection :
        IProjectionHandler<UserCreatedEvent>,
        IProjectionHandler<UserUpdatedEvent>
    {
        public ConcurrentDictionary<string, UserProfile> ProjectedUsers { get; } = new();

        public ValueTask ProjectAsync(UserCreatedEvent @event, CancellationToken ct = default)
        {
            ProjectedUsers[@event.UserId] = new UserProfile
            {
                UserId = @event.UserId,
                Name = @event.Name,
                Email = @event.Email
            };
            return ValueTask.CompletedTask;
        }

        public ValueTask ProjectAsync(UserUpdatedEvent @event, CancellationToken ct = default)
        {
            if (ProjectedUsers.TryGetValue(@event.UserId, out var profile))
            {
                profile.Name = @event.Name;
                profile.Email = @event.Email;
                ProjectedUsers[@event.UserId] = profile;
            }
            return ValueTask.CompletedTask;
        }

        public class UserProfile
        {
            public string UserId { get; set; } = "";
            public string Name { get; set; } = "";
            public string Email { get; set; } = "";
        }
    }

    // Test projection: Partitioned projection
    [Projection]
    public class TestUserStatisticsProjection : IPartitionedProjectionHandler<UserActivityEvent>
    {
        public ConcurrentDictionary<string, int> UserActivityCounts { get; } = new();

        public string GetPartitionKey(UserActivityEvent @event)
        {
            return @event.UserId;
        }

        public ValueTask ProjectAsync(UserActivityEvent @event, CancellationToken ct = default)
        {
            UserActivityCounts.AddOrUpdate(@event.UserId, 1, (_, count) => count + 1);
            return ValueTask.CompletedTask;
        }
    }
}
