# BbQ.Events.SourceGenerators

Source generators for BbQ.Events - Automatically discovers and registers event handlers and subscribers.

## Overview

This package contains Roslyn source generators that automatically discover implementations of `IEventHandler<TEvent>` and `IEventSubscriber<TEvent>` in your project and generate registration code.

## Features

- **Automatic Discovery**: Finds all event handlers and subscribers at compile-time
- **Zero Configuration**: No manual registration needed
- **Type-Safe**: Generates strongly-typed registration code
- **Performance**: No runtime reflection for handler discovery

## Installation

This package is automatically included when you reference BbQ.Events.

## Usage

The source generator automatically creates an extension method for your assembly:

```csharp
services.AddYourAssemblyNameEventHandlers();
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

// Registration (automatically generated)
services.AddInMemoryEventBus();
services.AddYourProjectNameEventHandlers(); // Auto-generated method
```

## Generated Code

The generator creates a static class in the `BbQ.Events.DependencyInjection` namespace with an extension method that registers all discovered handlers:

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
- C# 12.0 or later
- BbQ.Events package

## How It Works

The source generator:
1. Scans your project for classes implementing `IEventHandler<TEvent>` or `IEventSubscriber<TEvent>`
2. Generates registration code at compile-time
3. Creates an extension method specific to your assembly name
4. No runtime reflection or assembly scanning required

## License

This project is licensed under the MIT License.
