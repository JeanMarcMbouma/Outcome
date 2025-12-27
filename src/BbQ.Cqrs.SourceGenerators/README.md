# BbQ.Cqrs Source Generators

Source generators for BbQ.Cqrs that automatically generate handler registration and reduce boilerplate code.

## Features

- **Handler Registration Generator**: Automatically detects and registers `ICommandHandler` and `IQueryHandler` implementations
- **Opt-in Handler Stubs**: Generate handler stubs for classes marked with `[Command]` or `[Query]` attributes
- **Behavior Registration**: Automatically register pipeline behaviors marked with `[Behavior(Order = ...)]` attribute

## Installation

```bash
dotnet add package BbQ.Cqrs.SourceGenerators
```

## Usage

### Mark Commands and Queries

```csharp
[Command]
public class CreateUserCommand : ICommand<Outcome<User>>
{
    public string Email { get; set; }
    public string Name { get; set; }
}

[Query]
public class GetUserByIdQuery : IQuery<Outcome<User>>
{
    public Guid UserId { get; set; }
}
```

### Mark Behaviors for Automatic Registration

```csharp
[Behavior(Order = 1)]
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    // Implementation...
}
```

The source generator will automatically detect and register these components.
