# BbQ.Cqrs Source Generators

Source generators for BbQ.Cqrs that automatically detect and register handlers and behaviors, reducing boilerplate code.

## Features

- **Handler Registration Generator**: Automatically detects and registers handlers implementing `IRequestHandler<,>` and `IRequestHandler<>` for commands and queries
- **Behavior Registration**: Automatically register pipeline behaviors marked with `[Behavior(Order = ...)]` attribute in the specified order

## Installation

```bash
dotnet add package BbQ.Cqrs.SourceGenerators
```

## Usage

### Mark Commands and Queries (Optional)

The `[Command]` and `[Query]` attributes are optional markers for tooling/IDE support:

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

Handlers are automatically detected based on interface implementation, regardless of whether these attributes are present.

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

The source generator will automatically detect and register these components.
