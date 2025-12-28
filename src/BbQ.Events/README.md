# BbQ.Events

Event-driven architecture support with strongly-typed pub/sub for BbQ libraries.

## ✨ Features

- **Type-safe event publishing** with `IEventPublisher`
- **Event handlers** (`IEventHandler<TEvent>`) for processing events one-by-one
- **Event subscribers** (`IEventSubscriber<TEvent>`) for consuming event streams
- **In-memory event bus** for single-process applications
- **Thread-safe** implementation using `System.Threading.Channels`
- **Storage-agnostic** design - extend for distributed scenarios
- **Source generator support** - automatic handler/subscriber discovery when used with BbQ.Cqrs

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

## 🔗 Integration with BbQ.Cqrs

When used with BbQ.Cqrs, event handlers and subscribers are automatically discovered by source generators:

```csharp
services.AddInMemoryEventBus();
services.AddYourAssemblyNameHandlers();  // Auto-discovers event handlers/subscribers
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

### Event Bus
Central hub combining publishing and subscribing capabilities.

## ⚙️ Configuration

```csharp
// Default configuration
services.AddInMemoryEventBus();

// Manual handler registration
services.AddScoped<IEventHandler<MyEvent>, MyEventHandler>();
services.AddScoped<IEventSubscriber<MyEvent>, MyEventSubscriber>();
```

## 🎯 Design Principles

- **Optional consumers**: Events can be published without handlers or subscribers
- **Type safety**: Compile-time checking for all event types
- **Explicit**: Clear separation between publishing, handling, and subscribing
- **Storage-agnostic**: Interfaces can be implemented for any storage/messaging backend
- **Compatible**: Works standalone or integrates with BbQ.Cqrs

## 📄 License

This project is licensed under the MIT License.
