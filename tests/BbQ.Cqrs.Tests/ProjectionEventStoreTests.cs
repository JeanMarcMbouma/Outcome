using BbQ.Events;
using BbQ.Events.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.Collections.Concurrent;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Sample tests demonstrating projection behavior with InMemoryEventStore.
/// These tests show how to seed historical events and test projection replay.
/// </summary>
[TestFixture]
public class ProjectionEventStoreTests
{
    [TearDown]
    public void TearDown()
    {
        ProjectionHandlerRegistry.Clear();
        EventCountProjection.Clear();
        UserProjection.Clear();
    }

    [Test]
    public async Task EventStore_CanSeedAndReadEvents()
    {
        // Arrange
        var store = new InMemoryEventStore();

        // Act - Seed some events
        await store.SeedEventsAsync("users",
            new UserRegistered(1, "Alice"),
            new UserRegistered(2, "Bob"),
            new UserRegistered(3, "Charlie"));

        // Assert - Read them back
        var events = await store.ReadEventsAsync<UserRegistered>("users");
        
        Assert.That(events.Count, Is.EqualTo(3));
        Assert.That(events[0].Name, Is.EqualTo("Alice"));
        Assert.That(events[1].Name, Is.EqualTo("Bob"));
        Assert.That(events[2].Name, Is.EqualTo("Charlie"));
    }

    [Test]
    public async Task EventStore_SupportsPositionTracking()
    {
        // Arrange
        var store = new InMemoryEventStore();

        // Act - Append events and track positions
        var pos1 = await store.AppendAsync("users", new UserRegistered(1, "Alice"));
        var pos2 = await store.AppendAsync("users", new UserRegistered(2, "Bob"));
        var pos3 = await store.AppendAsync("users", new UserRegistered(3, "Charlie"));

        // Assert - Positions are sequential
        Assert.That(pos1, Is.EqualTo(0));
        Assert.That(pos2, Is.EqualTo(1));
        Assert.That(pos3, Is.EqualTo(2));

        // Can read from specific position
        var eventsFromPos1 = await store.ReadAllAsync<UserRegistered>("users", fromPosition: 1);
        Assert.That(eventsFromPos1.Count, Is.EqualTo(2));
        Assert.That(eventsFromPos1[0].Position, Is.EqualTo(1));
        Assert.That(eventsFromPos1[0].Event.Name, Is.EqualTo("Bob"));
    }

    [Test]
    public async Task Projection_CanReplayFromEventStore()
    {
        // Arrange - Seed historical events
        var store = new InMemoryEventStore();
        await store.SeedEventsAsync("users",
            new UserRegistered(1, "Alice"),
            new UserRegistered(2, "Bob"),
            new UserRegistered(3, "Charlie"),
            new UserRegistered(4, "Dave"),
            new UserRegistered(5, "Eve"));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IEventStore>(store);
        services.AddProjection<EventCountProjection>();

        var provider = services.BuildServiceProvider();
        var projection = provider.GetRequiredService<EventCountProjection>();

        // Act - Process events from store
        await foreach (var stored in store.ReadAsync<UserRegistered>("users"))
        {
            await projection.ProjectAsync(stored.Event, CancellationToken.None);
        }

        // Assert - All events processed
        Assert.That(projection.Count, Is.EqualTo(5));
    }

    [Test]
    public async Task Projection_CanReplayFromCheckpoint()
    {
        // Arrange - Seed historical events
        var store = new InMemoryEventStore();
        await store.SeedEventsAsync("users",
            new UserRegistered(1, "Alice"),
            new UserRegistered(2, "Bob"),
            new UserRegistered(3, "Charlie"),
            new UserRegistered(4, "Dave"),
            new UserRegistered(5, "Eve"));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IEventStore>(store);
        services.AddProjection<EventCountProjection>();

        var provider = services.BuildServiceProvider();
        var projection = provider.GetRequiredService<EventCountProjection>();

        // Act - Process only events after checkpoint position 2
        // (simulating resume after processing first 3 events: positions 0, 1, 2)
        await foreach (var stored in store.ReadAsync<UserRegistered>("users", fromPosition: 3))
        {
            await projection.ProjectAsync(stored.Event, CancellationToken.None);
        }

        // Assert - Only 2 events processed (positions 3 and 4)
        Assert.That(projection.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Projection_BuildsReadModel()
    {
        // Arrange - Seed user events
        var store = new InMemoryEventStore();
        await store.SeedEventsAsync<UserRegistered>("users",
            new UserRegistered(1, "Alice"),
            new UserRegistered(2, "Bob"),
            new UserRegistered(3, "Charlie"));
        
        await store.SeedEventsAsync<UserActivated>("users",
            new UserActivated(1),
            new UserActivated(3));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IEventStore>(store);
        services.AddProjection<UserProjection>();

        var provider = services.BuildServiceProvider();
        var projection = provider.GetRequiredService<UserProjection>();

        // Act - Process all events to build read model
        await foreach (var stored in store.ReadAsync<UserRegistered>("users"))
        {
            await projection.ProjectAsync(stored.Event, CancellationToken.None);
        }
        await foreach (var stored in store.ReadAsync<UserActivated>("users"))
        {
            await projection.ProjectAsync(stored.Event, CancellationToken.None);
        }

        // Assert - Read model reflects event history
        Assert.That(projection.Users.Count, Is.EqualTo(3));
        Assert.That(projection.Users[1].IsActive, Is.True);
        Assert.That(projection.Users[2].IsActive, Is.False);
        Assert.That(projection.Users[3].IsActive, Is.True);
    }

    [Test]
    public async Task EventStore_SupportsMultipleStreams()
    {
        // Arrange
        var store = new InMemoryEventStore();

        // Act - Add events to different streams
        await store.SeedEventsAsync("users",
            new UserRegistered(1, "Alice"),
            new UserRegistered(2, "Bob"));

        await store.SeedEventsAsync("orders",
            new OrderPlaced(101, 1),
            new OrderPlaced(102, 2));

        // Assert - Streams are independent
        var userEvents = await store.ReadEventsAsync<UserRegistered>("users");
        var orderEvents = await store.ReadEventsAsync<OrderPlaced>("orders");

        Assert.That(userEvents.Count, Is.EqualTo(2));
        Assert.That(orderEvents.Count, Is.EqualTo(2));

        var userPosition = await store.GetStreamPositionAsync("users");
        var orderPosition = await store.GetStreamPositionAsync("orders");

        Assert.That(userPosition, Is.EqualTo(1)); // 0 and 1
        Assert.That(orderPosition, Is.EqualTo(1)); // 0 and 1
    }

    // Test events
    public record UserRegistered(int UserId, string Name);
    public record UserActivated(int UserId);
    public record OrderPlaced(int OrderId, int UserId);

    // Test projections
    public class EventCountProjection : IProjectionHandler<UserRegistered>
    {
        private static int _count;

        public int Count => _count;

        public ValueTask ProjectAsync(UserRegistered @event, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _count);
            return ValueTask.CompletedTask;
        }

        public static void Clear() => _count = 0;
    }

    public class UserProjection : 
        IProjectionHandler<UserRegistered>,
        IProjectionHandler<UserActivated>
    {
        private static readonly ConcurrentDictionary<int, UserReadModel> _users = new();

        public ConcurrentDictionary<int, UserReadModel> Users => _users;

        public ValueTask ProjectAsync(UserRegistered @event, CancellationToken ct = default)
        {
            _users[@event.UserId] = new UserReadModel(@event.UserId, @event.Name, false);
            return ValueTask.CompletedTask;
        }

        public ValueTask ProjectAsync(UserActivated @event, CancellationToken ct = default)
        {
            if (_users.TryGetValue(@event.UserId, out var user))
            {
                _users[@event.UserId] = user with { IsActive = true };
            }
            return ValueTask.CompletedTask;
        }

        public static void Clear() => _users.Clear();
    }

    public record UserReadModel(int UserId, string Name, bool IsActive);
}
