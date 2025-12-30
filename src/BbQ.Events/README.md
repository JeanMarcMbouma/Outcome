# BbQ.Events

Event-driven architecture support with strongly-typed pub/sub and projections for BbQ libraries.

## ✨ Features

- **Type-safe event publishing** with `IEventPublisher`
- **Event handlers** (`IEventHandler<TEvent>`) for processing events one-by-one
- **Event subscribers** (`IEventSubscriber<TEvent>`) for consuming event streams
- **Projection support** for building read models and materialized views
- **Partitioned projections** for parallel event processing
- **Projection monitoring** via `IProjectionMonitor` for observability (events/sec, lag, worker count, checkpoints)
- **Projection replay API** via `IProjectionRebuilder` for resetting and rebuilding projections
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

### Error Handling & Retry Policies (NEW)

Configure how projections handle errors during event processing:

```csharp
services.AddProjection<UserProfileProjection>(options =>
{
    // Configure error handling strategy
    options.ErrorHandling.Strategy = ProjectionErrorHandlingStrategy.Retry;
    options.ErrorHandling.MaxRetryAttempts = 3;
    options.ErrorHandling.InitialRetryDelayMs = 1000; // 1 second
    options.ErrorHandling.MaxRetryDelayMs = 30000;    // 30 seconds (cap)
    options.ErrorHandling.FallbackStrategy = ProjectionErrorHandlingStrategy.Skip;
});
```

**Available Strategies:**

1. **Retry** (Default) - Retries with exponential backoff for transient failures
   - Attempts: `MaxRetryAttempts` (default: 3)
   - Delay: Starts at `InitialRetryDelayMs` (default: 1000ms), doubles each retry up to `MaxRetryDelayMs` (default: 30000ms)
   - After exhausting retries, uses `FallbackStrategy` (default: Skip)

2. **Skip** - Logs error and continues processing (event is marked as processed)
   - Best for non-critical events or when availability is more important than consistency
   - Event is checkpointed and won't be reprocessed

3. **Stop** - Halts the projection worker for manual intervention
   - Best when data consistency is critical
   - Worker must be manually restarted

**Examples:**

```csharp
// Retry transient failures, skip after exhaustion
services.AddProjection<UserProfileProjection>(options =>
{
    options.ErrorHandling.Strategy = ProjectionErrorHandlingStrategy.Retry;
    options.ErrorHandling.MaxRetryAttempts = 5;
    options.ErrorHandling.FallbackStrategy = ProjectionErrorHandlingStrategy.Skip;
});

// Skip failed events immediately
services.AddProjection<AnalyticsProjection>(options =>
{
    options.ErrorHandling.Strategy = ProjectionErrorHandlingStrategy.Skip;
});

// Stop on any error for critical projections
services.AddProjection<FinancialProjection>(options =>
{
    options.ErrorHandling.Strategy = ProjectionErrorHandlingStrategy.Stop;
});
```

**Structured Logging:**

All error handling strategies provide structured logging with event details:
- Event type and handler information
- Attempt count and retry delays
- Error messages and stack traces
- Projection and partition context

```csharp
// Example log output
// [Warning] Error processing event for UserProjection:user-123 at position 456. 
//           Attempt 2 of 3. Retrying in 2000ms
// [Error] Skipping failed event for AnalyticsProjection:_default at position 789. 
//         Event type: UserActivity, Handler: UserActivityProjection
// [Critical] Stopping projection worker for FinancialProjection:_default at position 1011. 
//            Manual intervention required.
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

if (metrics != null)
{
    Console.WriteLine($"Lag: {metrics.Lag} events");
    Console.WriteLine($"Throughput: {metrics.EventsPerSecond:F2} events/sec");
    Console.WriteLine($"Workers: {metrics.WorkerCount}");
    Console.WriteLine($"Checkpoints written: {metrics.CheckpointsWritten}");
}
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

### Projection Replay API

The projection rebuilder provides APIs to reset projection checkpoints and rebuild projections from scratch. This is useful for:
- Rebuilding projections after schema changes
- Recovering from corrupted projection state
- Testing projection logic
- Migrating to new projection implementations

The `IProjectionRebuilder` is automatically registered when you call `AddProjectionEngine()`.

#### Reset All Projections

```csharp
var rebuilder = serviceProvider.GetRequiredService<IProjectionRebuilder>();

// Reset all registered projections
await rebuilder.ResetAllProjectionsAsync(cancellationToken);

// Restart projection engine to begin rebuild
await engine.RunAsync(cancellationToken);
```

#### Reset a Single Projection

```csharp
var rebuilder = serviceProvider.GetRequiredService<IProjectionRebuilder>();

// Reset a specific projection
await rebuilder.ResetProjectionAsync("UserProfileProjection", cancellationToken);

// Restart projection engine to begin rebuild
await engine.RunAsync(cancellationToken);
```

#### Reset a Single Partition

For partitioned projections, you can reset individual partitions without affecting others:

```csharp
var rebuilder = serviceProvider.GetRequiredService<IProjectionRebuilder>();

// Reset a specific partition
await rebuilder.ResetPartitionAsync("UserStatisticsProjection", "user-123", cancellationToken);

// Restart projection engine to begin rebuild
await engine.RunAsync(cancellationToken);
```

#### List Registered Projections

Useful for CLI tools and management UIs:

```csharp
var rebuilder = serviceProvider.GetRequiredService<IProjectionRebuilder>();

// Get all registered projection names
var projections = rebuilder.GetRegisteredProjections();
foreach (var projection in projections)
{
    Console.WriteLine($"Projection: {projection}");
}
```

#### CLI-Friendly Usage

The rebuilder API is designed to be easily integrated into CLI applications:

```csharp
using BbQ.Events;
using BbQ.Events.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

// Configure services
var services = new ServiceCollection();
services.AddLogging();
services.AddInMemoryEventBus();
services.AddProjectionsFromAssembly(typeof(Program).Assembly);
services.AddProjectionEngine();

var provider = services.BuildServiceProvider();
var rebuilder = provider.GetRequiredService<IProjectionRebuilder>();

// Parse command-line arguments
if (args.Length == 0 || args[0] == "list")
{
    // List all projections
    Console.WriteLine("Registered projections:");
    foreach (var projection in rebuilder.GetRegisteredProjections())
    {
        Console.WriteLine($"  - {projection}");
    }
}
else if (args[0] == "reset-all")
{
    // Reset all projections
    Console.WriteLine("Resetting all projections...");
    await rebuilder.ResetAllProjectionsAsync();
    Console.WriteLine("All projections reset. Restart the projection engine to rebuild.");
}
else if (args[0] == "reset" && args.Length >= 2)
{
    // Reset a specific projection
    var projectionName = args[1];
    Console.WriteLine($"Resetting projection: {projectionName}...");
    await rebuilder.ResetProjectionAsync(projectionName);
    Console.WriteLine($"Projection {projectionName} reset. Restart the projection engine to rebuild.");
}
else if (args[0] == "reset-partition" && args.Length >= 3)
{
    // Reset a specific partition
    var projectionName = args[1];
    var partitionKey = args[2];
    Console.WriteLine($"Resetting partition {partitionKey} of projection {projectionName}...");
    await rebuilder.ResetPartitionAsync(projectionName, partitionKey);
    Console.WriteLine($"Partition {partitionKey} reset. Restart the projection engine to rebuild.");
}
else
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  list                           - List all registered projections");
    Console.WriteLine("  reset-all                      - Reset all projections");
    Console.WriteLine("  reset <projection-name>        - Reset a specific projection");
    Console.WriteLine("  reset-partition <projection-name> <partition-key> - Reset a specific partition");
}
```

**Note:** After resetting checkpoints, you must restart the projection engine (or projections) for the changes to take effect. The rebuilder only manages checkpoints - it does not modify projection state or read models directly.

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

### Projection Rebuilder
Manages projection checkpoints for rebuilding projections from scratch. Supports resetting all projections, single projections, or individual partitions.

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
