# BbQ.Events

Event-driven architecture support with strongly-typed pub/sub and projections for BbQ libraries.

## ✨ Features

- **Type-safe event publishing** with `IEventPublisher`
- **Event handlers** (`IEventHandler<TEvent>`) for processing events one-by-one
- **Event subscribers** (`IEventSubscriber<TEvent>`) for consuming event streams
- **Projection support** for building read models and materialized views
- **Partitioned projections** for parallel event processing
- **Projection monitoring** via `IProjectionMonitor` for observability (events/sec, lag, worker count, checkpoints)
- **In-memory event bus** for single-process applications
- **Thread-safe** implementation using `System.Threading.Channels`
- **Storage-agnostic** design - extend for distributed scenarios
- **Source generator support** - automatic handler/subscriber/projection discovery via BbQ.Events.SourceGenerators

## 📦 Installation

```bash
dotnet add package BbQ.Events
```

## 🚀 Quick Start

### 1. Register the Event Bus

```csharp
using BbQ.Events.DependencyInjection;

services.AddInMemoryEventBus();
```

### 2. Publish Events

```csharp
using BbQ.Events;

public class CreateUserHandler
{
    private readonly IEventPublisher _publisher;

    public CreateUserHandler(IEventPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task Handle(CreateUserCommand command)
    {
        // Domain logic...
        var user = new User(command.Id, command.Name);

        // Publish event
        await _publisher.Publish(new UserCreated(user.Id, user.Name));
    }
}
```

### 3. Handle Events

```csharp
public class SendWelcomeEmailHandler : IEventHandler<UserCreated>
{
    public Task Handle(UserCreated @event, CancellationToken ct)
    {
        Console.WriteLine($"Sending welcome email to {@event.Name}");
        return Task.CompletedTask;
    }
}

// Register manually
services.AddScoped<IEventHandler<UserCreated>, SendWelcomeEmailHandler>();
```

### 4. Subscribe to Event Streams

```csharp
public class UserAnalyticsSubscriber : IEventSubscriber<UserCreated>
{
    private readonly IEventBus _eventBus;

    public UserAnalyticsSubscriber(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public IAsyncEnumerable<UserCreated> Subscribe(CancellationToken ct)
        => _eventBus.Subscribe<UserCreated>(ct);
}

// Consume the stream
await foreach (var evt in subscriber.Subscribe(cancellationToken))
{
    // Process event
}
```

## 🎯 Projections (NEW)

Projections transform events into queryable read models for event-sourced systems.

### Define a Projection

```csharp
[Projection]
public class UserProfileProjection :
    IProjectionHandler<UserCreated>,
    IProjectionHandler<UserUpdated>
{
    private readonly IUserRepository _repository;
    
    public UserProfileProjection(IUserRepository repository)
    {
        _repository = repository;
    }
    
    public async ValueTask ProjectAsync(UserCreated evt, CancellationToken ct)
    {
        var profile = new UserProfile(evt.UserId, evt.Name, evt.Email);
        await _repository.UpsertAsync(profile, ct);
    }
    
    public async ValueTask ProjectAsync(UserUpdated evt, CancellationToken ct)
    {
        var profile = await _repository.GetAsync(evt.UserId, ct);
        if (profile != null)
        {
            profile.Name = evt.Name;
            await _repository.UpsertAsync(profile, ct);
        }
    }
}
```

### Register and Run Projections

```csharp
// Register event bus and projections
services.AddInMemoryEventBus();
services.AddProjectionsFromAssembly(typeof(Program).Assembly);
services.AddProjectionEngine();

// Run projection engine (as hosted service or manually)
var engine = serviceProvider.GetRequiredService<IProjectionEngine>();
await engine.RunAsync(cancellationToken);
```

### Partitioned Projections

Use `IPartitionedProjectionHandler<TEvent>` to specify partition keys for ordering guarantees:

```csharp
[Projection]
public class UserStatisticsProjection : IPartitionedProjectionHandler<UserActivity>
{
    public string GetPartitionKey(UserActivity evt) => evt.UserId.ToString();
    
    public async ValueTask ProjectAsync(UserActivity evt, CancellationToken ct)
    {
        // Process event - partition key can be used for custom parallelization
    }
}
```

**Note:** The default projection engine processes events sequentially. Implement a custom IProjectionEngine to leverage partition keys for parallel processing.

📖 **See [PROJECTION_SAMPLE.md](PROJECTION_SAMPLE.md) for complete examples and best practices.**

## 🔗 Automatic Handler Registration

Event handlers, subscribers, and projections are automatically discovered by the BbQ.Events source generator:

```csharp
services.AddInMemoryEventBus();
services.AddYourAssemblyNameEventHandlers();  // Auto-discovers event handlers/subscribers
services.AddYourAssemblyNameProjections();    // Auto-discovers projections
```

## 🔗 Integration with BbQ.Cqrs (Optional)

BbQ.Events works standalone, but can be easily integrated with BbQ.Cqrs for complete event-driven CQRS:

```csharp
// Command handler that publishes events
public class CreateUserCommandHandler : IRequestHandler<CreateUser, Outcome<User>>
{
    private readonly IEventPublisher _publisher;

    public async Task<Outcome<User>> Handle(CreateUser command, CancellationToken ct)
    {
        var user = new User(command.Id, command.Name);
        
        // Publish event after state change
        await _publisher.Publish(new UserCreated(user.Id, user.Name), ct);
        
        return Outcome<User>.From(user);
    }
}
```

## 🏗️ Architecture

### In-Memory Event Bus

The default `InMemoryEventBus` implementation:
- Uses `System.Threading.Channels` for thread-safe pub/sub
- Supports multiple concurrent handlers per event type
- Supports multiple concurrent subscribers per event type
- Handlers are executed and awaited before publish completes
- Slow subscribers don't block publishers (drops oldest messages)
- Suitable for single-process applications

### Projection Engine

The default projection engine:
- Subscribes to live event streams and dispatches to projection handlers
- Processes events sequentially as they arrive
- Provides checkpoint storage infrastructure (IProjectionCheckpointStore)
- Handles errors gracefully and continues processing
- Can be extended for batch processing, parallel processing, and automatic checkpointing
- **Tracks metrics and health via IProjectionMonitor** (events/sec, lag, worker count, checkpoints)

### Projection Monitoring

The projection monitoring system tracks:
- **Events processed per second** - throughput metrics
- **Per-partition lag** - how far behind the projection is
- **Active worker count** - number of concurrent workers
- **Checkpoint frequency** - how often checkpoints are written

Example usage:
```csharp
// Monitor is automatically registered with AddProjectionEngine()
services.AddProjectionEngine();

// Query metrics at runtime
var monitor = serviceProvider.GetRequiredService<IProjectionMonitor>();
var metrics = monitor.GetMetrics("UserProjection", "_default");

Console.WriteLine($"Lag: {metrics.Lag} events");
Console.WriteLine($"Throughput: {metrics.EventsPerSecond:F2} events/sec");
Console.WriteLine($"Workers: {metrics.WorkerCount}");
Console.WriteLine($"Checkpoints written: {metrics.CheckpointsWritten}");
```

For production monitoring, implement a custom `IProjectionMonitor`:
```csharp
public class PrometheusProjectionMonitor : IProjectionMonitor
{
    private readonly Counter _eventsProcessed = Metrics.CreateCounter(
        "projection_events_processed_total", 
        "Total events processed by projection",
        new CounterConfiguration { LabelNames = new[] { "projection", "partition" } });
    
    private readonly Gauge _lag = Metrics.CreateGauge(
        "projection_lag", 
        "Lag between current and latest position",
        new GaugeConfiguration { LabelNames = new[] { "projection", "partition" } });
    
    public void RecordEventProcessed(string projectionName, string partitionKey, long currentPosition)
    {
        _eventsProcessed.WithLabels(projectionName, partitionKey).Inc();
    }
    
    public void RecordLag(string projectionName, string partitionKey, long currentPosition, long? latestPosition)
    {
        if (latestPosition.HasValue)
        {
            var lag = Math.Max(0, latestPosition.Value - currentPosition);
            _lag.WithLabels(projectionName, partitionKey).Set(lag);
        }
    }
    
    // Implement other methods...
}

// Register custom monitor
services.AddSingleton<IProjectionMonitor, PrometheusProjectionMonitor>();
services.AddProjectionEngine();
```

### Distributed Systems

For multi-process or distributed systems, implement `IEventBus` with your preferred message broker:
- RabbitMQ
- Azure Service Bus
- Apache Kafka
- AWS SQS/SNS

## 📚 Key Concepts

### Event Publisher
Publishes events to all registered handlers and active subscribers.

### Event Handler
Processes events one-by-one as they're published. Multiple handlers can process the same event.

### Event Subscriber
Provides a stream of events for reactive programming patterns. Each subscriber gets an independent stream.

### Projection Handler
Transforms events into read models. Can handle multiple event types and optionally support partitioning.

### Projection Engine
Orchestrates projection execution from live event streams. Provides infrastructure for checkpointing via IProjectionCheckpointStore.

### Event Bus
Central hub combining publishing and subscribing capabilities.

## ⚙️ Configuration

```csharp
// Default configuration
services.AddInMemoryEventBus();

// Manual handler registration
services.AddScoped<IEventHandler<MyEvent>, MyEventHandler>();
services.AddScoped<IEventSubscriber<MyEvent>, MyEventSubscriber>();

// Projection registration
services.AddProjection<MyProjection>();
services.AddProjectionsFromAssembly(typeof(Program).Assembly);
services.AddProjectionEngine();

// Custom checkpoint store for production
services.AddSingleton<IProjectionCheckpointStore, SqlCheckpointStore>();
```

## 🎯 Design Principles

- **Optional consumers**: Events can be published without handlers or subscribers
- **Type safety**: Compile-time checking for all event types
- **Explicit**: Clear separation between publishing, handling, subscribing, and projecting
- **Storage-agnostic**: Interfaces can be implemented for any storage/messaging backend
- **Extensible**: Default implementations can be extended for production features (checkpointing, batching, parallelism)
- **Compatible**: Works standalone or integrates with BbQ.Cqrs

## 📄 License

This project is licensed under the MIT License.
