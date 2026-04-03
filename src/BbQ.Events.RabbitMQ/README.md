# BbQ.Events.RabbitMQ

RabbitMQ implementation of IEventBus for BbQ.Events, providing distributed pub/sub messaging.

This package provides a production-ready, distributed event bus implementation for:
- **Distributed Pub/Sub**: Cross-process event publishing and subscription via RabbitMQ
- **IEventBus**: Drop-in replacement for the in-memory event bus

## Features

- ✅ **Distributed Messaging**: Events are delivered across multiple processes and services
- ✅ **Topic Exchange**: Routing key-based event delivery using event type names
- ✅ **Durable Messages**: Persistent messages survive broker restarts
- ✅ **Thread-Safe**: Safe for concurrent publishing and subscribing
- ✅ **JSON Serialization**: Cross-process event serialization
- ✅ **Local Handler Support**: IEventHandler instances are still executed locally on publish
- ✅ **Automatic Cleanup**: Subscriber queues are cleaned up on cancellation
- ✅ **Feature-Based Architecture**: Organized by capability (Events, Configuration, Internal)

## Installation

```bash
dotnet add package BbQ.Events.RabbitMQ
```

## Prerequisites

- RabbitMQ server accessible from your application
- .NET 8.0, .NET 9.0, or .NET 10.0

## Usage

### Basic Setup with Connection URI

```csharp
using BbQ.Events.RabbitMQ.Configuration;

var services = new ServiceCollection();

// Register RabbitMQ event bus
services.UseRabbitMqEventBus("amqp://guest:guest@localhost:5672/");
```

### Setup with Options

Configure advanced options:

```csharp
services.UseRabbitMqEventBus(options =>
{
    options.HostName = "rabbitmq-server";
    options.Port = 5672;
    options.UserName = "myuser";
    options.Password = "mypassword";
    options.VirtualHost = "/";
    options.ExchangeName = "my-app.events";
    options.DurableQueues = true;
    options.PersistentMessages = true;
    options.JsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
});
```

### Publishing Events

```csharp
var eventBus = provider.GetRequiredService<IEventBus>();

// Publish an event (delivered to local handlers and RabbitMQ)
await eventBus.Publish(new OrderCreated(orderId, customerId));
```

### Subscribing to Events

```csharp
var eventBus = provider.GetRequiredService<IEventBus>();

// Subscribe to events from RabbitMQ
await foreach (var @event in eventBus.Subscribe<OrderCreated>(cancellationToken))
{
    Console.WriteLine($"Order created: {@event.OrderId}");
}
```

### Using with Projections

```csharp
using BbQ.Events.Configuration;
using BbQ.Events.RabbitMQ.Configuration;

var services = new ServiceCollection();

// Register RabbitMQ event bus (replaces in-memory event bus)
services.UseRabbitMqEventBus("amqp://guest:guest@localhost:5672/");

// Register projections
services.AddProjection<OrderProjection>();

// Register projection engine
services.AddProjectionEngine();
```

### Connection String Configuration

Recommended: Store connection settings in configuration:

```csharp
var rabbitUri = builder.Configuration.GetConnectionString("RabbitMQ");
services.UseRabbitMqEventBus(rabbitUri);
```

**appsettings.json:**
```json
{
  "ConnectionStrings": {
    "RabbitMQ": "amqp://guest:guest@localhost:5672/"
  }
}
```

## Architecture

The package follows a feature-based folder structure:

```
BbQ.Events.RabbitMQ/
  Events/                     # Event bus implementation
    RabbitMqEventBus.cs
    RabbitMqEventBusOptions.cs
  
  Configuration/              # DI extensions
    ServiceCollectionExtensions.cs
  
  Internal/                   # Internal helpers (not public API)
    RabbitMqConstants.cs
```

This structure:
- Aligns with the BbQ.Events core library architecture
- Matches the SqlServer and PostgreSql library patterns
- Makes it easy to find related functionality
- Separates concerns cleanly
- Provides clear separation between public API and internal implementation

## RabbitMQ Topology

The event bus creates the following RabbitMQ resources:

### Exchange
- **Name**: Configurable via `ExchangeName` (default: "bbq.events")
- **Type**: Topic
- **Durable**: Yes

### Subscriber Queues
- **Name**: `{QueuePrefix}.{EventTypeName}.{UniqueId}`
- **Exclusive**: Yes (tied to the subscriber connection)
- **Auto-Delete**: Yes (cleaned up when subscriber disconnects)
- **Binding**: Routing key = event type full name

### Message Properties
- **Content-Type**: application/json
- **Delivery Mode**: Persistent (configurable)
- **Message ID**: Unique GUID per message
- **Type**: Event type full name
- **Custom Header**: `bbq-event-type` with event type full name

## Migration from InMemoryEventBus

Replace the in-memory event bus with RabbitMQ:

**Before:**
```csharp
services.AddInMemoryEventBus();
```

**After:**
```csharp
services.UseRabbitMqEventBus("amqp://guest:guest@localhost:5672/");
```

All existing event publishing and subscription code remains unchanged.

## Troubleshooting

### Connection Issues

If you encounter connection errors:

1. **Verify RabbitMQ is running**:
   ```bash
   rabbitmqctl status
   ```

2. **Check connection parameters**: Ensure hostname, port, username, and password are correct

3. **Check virtual host**: Ensure the virtual host exists and the user has access

4. **Check firewall**: Ensure port 5672 (or your configured port) is open

### Messages Not Being Delivered

1. **Check exchange exists**: Use RabbitMQ Management UI to verify the exchange is created

2. **Check queue bindings**: Verify subscriber queues are bound with the correct routing key

3. **Enable logging**: Increase log level to Debug for diagnostic messages

### Serialization Issues

If events are not deserializing correctly:

1. **Ensure consistent types**: Publisher and subscriber must use the same event type
2. **Check JSON options**: Ensure both sides use compatible serialization settings
3. **Check for assembly differences**: Event types must be identical across services

## License

MIT License - see LICENSE.txt for details

## Contributing

Contributions are welcome! Please open an issue or pull request at:
https://github.com/JeanMarcMbouma/Outcome
