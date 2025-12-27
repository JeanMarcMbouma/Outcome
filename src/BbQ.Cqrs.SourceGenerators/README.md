# BbQ.Cqrs Source Generators

Source generators and analyzers for BbQ.Cqrs that automatically detect and register handlers and behaviors, reducing boilerplate code.

## Features

- **Handler Registration Generator**: Automatically detects and registers handlers implementing `IRequestHandler<,>` and `IRequestHandler<>` for commands and queries
- **Behavior Registration**: Automatically register pipeline behaviors marked with `[Behavior(Order = ...)]` attribute in the specified order
- **Analyzer**: Prevents misuse of `[Behavior]` attribute on behaviors with more than 2 type parameters

## Installation

```bash
dotnet add package BbQ.Cqrs.SourceGenerators
```

## Usage

### Automatic Handler Detection

Handlers are automatically detected based on interface implementation. No attributes needed:

```csharp
public class CreateUserCommand : ICommand<Outcome<User>>
{
    public string Email { get; set; }
    public string Name { get; set; }
}

// Handler is automatically detected
public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Outcome<User>>
{
    // Implementation...
}
```

### Mark Behaviors for Automatic Registration

Only behaviors with the `[Behavior]` attribute are automatically registered:

```csharp
[Behavior(Order = 1)]
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    // Implementation...
}
```

**Important:** The `[Behavior]` attribute can only be used on behaviors with exactly 2 type parameters. Behaviors with additional type parameters (e.g., 3 or more) will trigger a compile-time error and must be registered manually.

## Analyzer Rules

### BBQCQRS001: Behavior attribute on class with incompatible type parameter count

This analyzer ensures that the `[Behavior]` attribute is only used on classes with exactly 2 type parameters that match `IPipelineBehavior<TRequest, TResponse>`.

**Invalid:**
```csharp
// ❌ Error: ValidationBehavior has 3 type parameters
[Behavior(Order = 1)]
public class ValidationBehavior<TRequest, TResponse, TPayload> : IPipelineBehavior<TRequest, TResponse>
{
    // This will cause a compile error
}
```

**Valid:**
```csharp
// ✅ Correct: LoggingBehavior has exactly 2 type parameters
[Behavior(Order = 1)]
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    // This will compile and register correctly
}
```

The source generator will automatically detect and register these components.
