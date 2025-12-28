# BbQ Libraries - Functional Result Types, CQRS & Events

A comprehensive suite of modern C# libraries for functional error handling, command-query responsibility segregation, and event-driven architecture patterns.

## 📦 Packages

### [BbQ.Outcome](./src/Outcome/README.md)
A modern C# functional result type for error-aware programming.

- **Structured errors** with `Code`, `Description`, and `Severity`
- **Async composition** with `BindAsync`, `MapAsync`, `CombineAsync`
- **LINQ integration** with native `Select`/`SelectMany` support
- **Source generator** support for auto-generating error helpers
- **Multi-targeting** across `netstandard2.0`, `net6.0`, and `net8.0`

```csharp
dotnet add package BbQ.Outcome
```

[📖 Full Documentation 📖](./src/Outcome/README.md)

### [BbQ.Cqrs](./src/BbQ.Cqrs/README.md)
A lightweight, extensible CQRS implementation that integrates seamlessly with Outcome.

- **Type-safe mediator** for commands and queries
- **Unified pipeline behaviors** for both regular and streaming requests
- **Streaming handlers** for processing large datasets with `IAsyncEnumerable<T>`
- **Specialized dispatchers** (`ICommandDispatcher`, `IQueryDispatcher`) for explicit CQRS separation
- **Source generators** for automatic handler registration, behavior registration
- **Test utilities** with `TestMediator` and `StubHandler`
- **Comprehensive documentation** on all interfaces and classes
- **Seamless integration** with `Outcome<T>` for error handling

```csharp
dotnet add package BbQ.Cqrs
```

[📖 Full Documentation 📖](./src/BbQ.Cqrs/README.md)

### [BbQ.Events](./src/BbQ.Events/README.md)
Event-driven architecture support with strongly-typed pub/sub patterns.

- **Type-safe event publishing** with `IEventPublisher`
- **Event handlers** (`IEventHandler<TEvent>`) for processing events one-by-one
- **Event subscribers** (`IEventSubscriber<TEvent>`) for consuming event streams
- **In-memory event bus** for single-process applications
- **Thread-safe** implementation using `System.Threading.Channels`
- **Storage-agnostic** design - extend for distributed scenarios
- **Source generator support** - automatic handler/subscriber discovery
- **Fully independent** - works standalone or with BbQ.Cqrs

```csharp
dotnet add package BbQ.Events
```

[📖 Full Documentation 📖](./src/BbQ.Events/README.md)

## 🚀 Quick Start

### Using Outcome
```csharp
var result = await GetUserAsync(userId);

return result.Match(
    onSuccess: user => Ok(user),
    onError: errors => BadRequest(new { errors })
);
```

### Using Outcome + CQRS
```csharp
// Define error codes
[QbqOutcome]
public enum UserErrorCode
{
    [Description("User not found")]
    NotFound,
    [Description("Email already in use")]
    EmailAlreadyExists
}

// Define a command
public class CreateUserCommand : ICommand<Outcome<User>>
{
    public string Email { get; set; }
    public string Name { get; set; }
}

// Implement a handler
public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Outcome<User>>
{
    public async Task<Outcome<User>> Handle(CreateUserCommand request, CancellationToken ct)
    {
        // Implementation...
    }
}

// Register and use
services.AddBbQMediator(typeof(Program).Assembly);

var result = await mediator.Send(new CreateUserCommand { Email = "test@example.com", Name = "Test" });
```

### Using Events
```csharp
// Register event bus
services.AddInMemoryEventBus();
services.AddYourAssemblyEventHandlers(); // Auto-discovers handlers

// Define an event
public record UserCreated(Guid Id, string Name);

// Publish event
await eventPublisher.Publish(new UserCreated(userId, userName));

// Handle event (auto-discovered)
public class SendWelcomeEmailHandler : IEventHandler<UserCreated>
{
    public Task Handle(UserCreated @event, CancellationToken ct)
    {
        // Send email...
        return Task.CompletedTask;
    }
}
```

## 💾 Installation

```bash
# Core error handling
dotnet add package BbQ.Outcome

# CQRS pattern support
dotnet add package BbQ.Cqrs

# Event-driven architecture
dotnet add package BbQ.Events
```

## 🔗 Integration

These libraries work best together:

```csharp
// Error codes are auto-generated with source generator
[QbqOutcome]
public enum DomainErrors
{
    [Description("Invalid input")]
    [ErrorSeverity(ErrorSeverity.Validation)]
    ValidationFailed,

    [Description("Not found")]
    NotFound
}

// Commands/queries return Outcome<T>
public class GetUserQuery : IQuery<Outcome<User>> { }

// Handlers use auto-generated errors
public class GetUserQueryHandler : IRequestHandler<GetUserQuery, Outcome<User>>
{
    public async Task<Outcome<User>> Handle(GetUserQuery request, CancellationToken ct)
    {
        var user = await _repository.GetAsync(request.UserId);
        return user == null 
            ? DomainErrorsErrors.NotFoundError.ToOutcome<User>()
            : Outcome<User>.From(user);
    }
}
```

## ✨ Key Features

| Feature | Outcome | CQRS | Events |
|---------|---------|------|--------|
| Structured error handling | ✅ | ✅ | - |
| Async composition | ✅ | ✅ | ✅ |
| Source-generated helpers | ✅ | ✅ | ✅ |
| LINQ integration | ✅ | - | - |
| Mediator pattern | - | ✅ | - |
| Pipeline behaviors | - | ✅ | - |
| Streaming handlers | - | ✅ | ✅ |
| Type-safe commands/queries | - | ✅ | - |
| Event publishing | - | - | ✅ |
| Event handlers | - | - | ✅ |
| Event subscribers | - | - | ✅ |
| Thread-safe in-memory bus | - | - | ✅ |
| Storage-agnostic design | - | - | ✅ |
| Fully independent | ✅ | ✅ | ✅ |
| Test utilities | - | ✅ | - |

## 📚 Documentation

- **[BbQ.Outcome Documentation](./src/Outcome/README.md)** - Complete guide to using Outcome for functional error handling
- **[BbQ.Cqrs Documentation](./src/BbQ.Cqrs/README.md)** - Complete guide to CQRS pattern implementation
- **[BbQ.Events Documentation](./src/BbQ.Events/README.md)** - Complete guide to event-driven architecture
- **[Strongly Typed Errors Guide](./STRONGLY_TYPED_ERRORS.md)** - Best practices for error handling patterns

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.


