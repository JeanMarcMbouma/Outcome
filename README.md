# BbQ Libraries - Functional Result Types & CQRS

A comprehensive suite of modern C# libraries for functional error handling and command-query responsibility segregation patterns.

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
- **Source generators** for automatic handler registration, behavior registration, and handler stubs
- **Test utilities** with `TestMediator` and `StubHandler`
- **Comprehensive documentation** on all interfaces and classes
- **Seamless integration** with `Outcome<T>` for error handling

```csharp
dotnet add package BbQ.Cqrs
```

[📖 Full Documentation 📖](./src/BbQ.Cqrs/README.md)

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

## 💾 Installation

```bash
# Core error handling
dotnet add package BbQ.Outcome

# CQRS pattern support
dotnet add package BbQ.Cqrs
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

| Feature | Outcome | CQRS |
|---------|---------|------|
| Structured error handling | ✅ | ✅ |
| Async composition | ✅ | ✅ |
| Source-generated error helpers | ✅ | ✅ |
| LINQ integration | ✅ | - |
| Mediator pattern | - | ✅ |
| Pipeline behaviors (regular & streaming) | - | ✅ |
| Streaming handlers with `IAsyncEnumerable` | - | ✅ |
| Specialized dispatchers | - | ✅ |
| Type-safe commands/queries | - | ✅ |
| Source-generated handler registration | - | ✅ |
| Test utilities | - | ✅ |

## 📚 Documentation

- **[BbQ.Outcome Documentation](./src/Outcome/README.md)** - Complete guide to using Outcome for functional error handling
- **[BbQ.Cqrs Documentation](./src/BbQ.Cqrs/README.md)** - Complete guide to CQRS pattern implementation
- **[Strongly Typed Errors Guide](./STRONGLY_TYPED_ERRORS.md)** - Best practices for error handling patterns

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.


