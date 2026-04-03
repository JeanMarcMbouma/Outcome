# BbQ.Events.SourceGenerators

Source generators for BbQ.Events - Automatically discovers and registers event handlers, subscribers, and projection handlers.

## Overview

This package contains Roslyn source generators that automatically discover implementations of `IEventHandler<TEvent>`, `IEventSubscriber<TEvent>`, `IProjectionHandler<TEvent>`, `IPartitionedProjectionHandler<TEvent>`, and `IProjectionBatchHandler<TEvent>` in your project and generate registration code.

## Features

- **Automatic Discovery**: Finds all event handlers, subscribers, and projection handlers at compile-time
- **Zero Configuration**: No manual registration needed
- **Type-Safe**: Generates strongly-typed registration code
- **Performance**: No runtime reflection for handler discovery
- **Projection Support**: Discovers and registers all three projection handler types

## Installation

This package is automatically included when you reference BbQ.Events.

## Usage

The source generator automatically creates extension methods for your assembly:

```csharp
services.AddYourAssemblyNameEventHandlers();  // Registers event handlers and subscribers
services.AddYourAssemblyNameProjections();    // Registers projection handlers
```

### Example

```csharp
// Your event handler
public class SendWelcomeEmailHandler : IEventHandler<UserCreated>
{
    public Task Handle(UserCreated @event, CancellationToken ct)
    {
        // Handle the event
        return Task.CompletedTask;
    }
}

// Your event subscriber
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

// Your projection handler (also auto-discovered)
[Projection("UserProfile")]
public class UserProfileProjection : IProjectionHandler<UserCreated>
{
    public Task HandleAsync(UserCreated @event, CancellationToken ct)
    {
        // Update read model...
        return Task.CompletedTask;
    }
}

// Registration (automatically generated)
services.AddInMemoryEventBus();
services.AddYourProjectNameEventHandlers(); // Auto-generated method
services.AddYourProjectNameProjections();   // Auto-generated method for projections
```

## Generated Code

The generator creates a static class in the `BbQ.Events.Configuration` namespace with extension methods that register all discovered handlers and projections:

```csharp
public static class GeneratedYourAssemblyEventRegistrationExtensions
{
    public static IServiceCollection AddYourAssemblyEventHandlers(
        this IServiceCollection services,
        ServiceLifetime handlersLifetime = ServiceLifetime.Scoped)
    {
        // Registers all IEventHandler<TEvent> implementations
        // Registers all IEventSubscriber<TEvent> implementations
        return services;
    }

    public static IServiceCollection AddYourAssemblyProjections(
        this IServiceCollection services)
    {
        // Registers all IProjectionHandler<TEvent> implementations
        // Registers all IPartitionedProjectionHandler<TEvent> implementations
        // Registers all IProjectionBatchHandler<TEvent> implementations
        return services;
    }
}
```

## Customization

### Handler Lifetime

By default, handlers are registered with `ServiceLifetime.Scoped`. You can change this:

```csharp
services.AddYourAssemblyEventHandlers(ServiceLifetime.Transient);
```

## Requirements

- .NET 8.0 or later
- C# 11 or later
- BbQ.Events package

## How It Works

The source generator:
1. Scans your project for classes implementing `IEventHandler<TEvent>`, `IEventSubscriber<TEvent>`, `IProjectionHandler<TEvent>`, `IPartitionedProjectionHandler<TEvent>`, or `IProjectionBatchHandler<TEvent>`
2. Generates registration code at compile-time
3. Creates extension methods specific to your assembly name
4. No runtime reflection or assembly scanning required

## License

This project is licensed under the MIT License.
