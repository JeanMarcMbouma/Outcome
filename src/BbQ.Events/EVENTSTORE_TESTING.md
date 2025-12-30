# InMemoryEventStore for Testing Projections

The `InMemoryEventStore` provides a simple, in-memory implementation of an event store designed for testing projection engines without external dependencies.

## Features

- ✅ **Position Tracking**: Events are stored with sequential positions
- ✅ **Checkpoint Replay**: Read events starting from any position
- ✅ **Multiple Streams**: Support for multiple independent event streams
- ✅ **Thread-Safe**: Safe for concurrent reads and writes
- ✅ **Test Helpers**: Extension methods for easy test setup

## Basic Usage

### Seeding Events

```csharp
var store = new InMemoryEventStore();

// Seed events one at a time
await store.AppendAsync("users", new UserRegistered(1, "Alice"));
await store.AppendAsync("users", new UserRegistered(2, "Bob"));

// Or seed multiple events at once
await store.SeedEventsAsync("users",
    new UserRegistered(3, "Charlie"),
    new UserRegistered(4, "Dave"));
```

### Reading Events

```csharp
// Read all events from beginning
await foreach (var stored in store.ReadAsync<UserRegistered>("users"))
{
    Console.WriteLine($"Position {stored.Position}: {stored.Event.Name}");
}

// Read from a specific position (checkpoint)
await foreach (var stored in store.ReadAsync<UserRegistered>("users", fromPosition: 5))
{
    // Process events after position 5
}

// Read all events into a list
var events = await store.ReadAllAsync<UserRegistered>("users");

// Read just the event data (without positions)
var eventData = await store.ReadEventsAsync<UserRegistered>("users");
```

### Testing Projection Replay

```csharp
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

    var projection = new UserCountProjection();

    // Act - Simulate resuming from checkpoint at position 2
    // This processes only events at positions 3, 4 (skipping 0, 1, 2)
    await foreach (var stored in store.ReadAsync<UserRegistered>("users", fromPosition: 3))
    {
        await projection.ProjectAsync(stored.Event);
    }

    // Assert
    Assert.That(projection.Count, Is.EqualTo(2)); // Only 2 events processed
}
```

### Testing with Projection Engine

```csharp
[Test]
public async Task ProjectionEngine_ProcessesHistoricalEvents()
{
    // Arrange - Seed events
    var store = new InMemoryEventStore();
    await store.SeedEventsAsync("orders",
        new OrderPlaced(1, 100),
        new OrderPlaced(2, 200),
        new OrderPlaced(3, 150));

    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSingleton<IEventStore>(store);
    services.AddProjection<OrderStatisticsProjection>();
    
    // Register projection engine (if needed)
    services.AddProjectionEngine();

    var provider = services.BuildServiceProvider();

    // Act - Process all historical events
    var projection = provider.GetRequiredService<OrderStatisticsProjection>();
    await foreach (var stored in store.ReadAsync<OrderPlaced>("orders"))
    {
        await projection.ProjectAsync(stored.Event);
    }

    // Assert
    Assert.That(projection.TotalOrders, Is.EqualTo(3));
    Assert.That(projection.TotalValue, Is.EqualTo(450));
}
```

## Helper Methods

The `EventStoreTestHelpers` class provides convenient extension methods:

### SeedEventsAsync
```csharp
// Seed multiple events at once
await store.SeedEventsAsync("users",
    new UserRegistered(1, "Alice"),
    new UserRegistered(2, "Bob"));

// Returns array of positions
var positions = await store.SeedEventsAsync("users", events);
```

### ReadAllAsync
```csharp
// Read all events into a list (with positions)
var storedEvents = await store.ReadAllAsync<UserRegistered>("users");
foreach (var stored in storedEvents)
{
    Console.WriteLine($"Position: {stored.Position}, User: {stored.Event.Name}");
}
```

### ReadEventsAsync
```csharp
// Read just the event data (without positions)
var events = await store.ReadEventsAsync<UserRegistered>("users");
```

### CountEventsAsync
```csharp
// Count events in a stream
var count = await store.CountEventsAsync<UserRegistered>("users");
```

## Multiple Streams

The event store supports multiple independent streams:

```csharp
var store = new InMemoryEventStore();

// Different streams with independent positions
await store.SeedEventsAsync("users",
    new UserRegistered(1, "Alice"),
    new UserRegistered(2, "Bob"));

await store.SeedEventsAsync("orders",
    new OrderPlaced(101, 1),
    new OrderPlaced(102, 2));

// Each stream has its own position counter starting from 0
var userPos = await store.GetStreamPositionAsync("users");  // Returns 1
var orderPos = await store.GetStreamPositionAsync("orders"); // Returns 1
```

## Position Semantics

- Positions start at `0` for the first event in each stream
- Positions are sequential within a stream: 0, 1, 2, 3, ...
- `fromPosition` parameter is **inclusive** - position 5 will include the event at position 5
- Use checkpoint + 1 if you want to skip already-processed events

## Comparison with InMemoryEventBus

| Feature | InMemoryEventStore | InMemoryEventBus |
|---------|-------------------|------------------|
| **Purpose** | Testing with historical events | Live pub/sub messaging |
| **Events Persist** | ✅ Yes, until cleared | ❌ No, only live delivery |
| **Position Tracking** | ✅ Yes | ❌ No |
| **Replay from Checkpoint** | ✅ Yes | ❌ No |
| **Subscribe to Live Events** | ❌ No (snapshot only) | ✅ Yes |
| **Best For** | Testing projection replay | Real-time event handling |

## Test Cleanup

Remember to clear the store between tests if reusing the same instance:

```csharp
[TearDown]
public void TearDown()
{
    if (_store is InMemoryEventStore inMemoryStore)
    {
        inMemoryStore.Clear();
    }
}
```

## Examples

See `ProjectionEventStoreTests.cs` for complete working examples demonstrating:
- Seeding and reading events
- Position tracking
- Projection replay from checkpoints
- Building read models
- Multiple stream support

## Production Use

For production scenarios, implement `IEventStore` with a persistent event store such as:
- EventStoreDB
- SQL Server with custom event tables
- Azure Event Hubs
- Apache Kafka

The interface is designed to be easily implemented with any durable event store technology.
