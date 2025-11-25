# Outcome

Outcome is a modern C# functional result type inspired by [ErrorOr](https://github.com/amantinband/error-or).  
It builds on the excellent foundation of ErrorOr by adding **async workflows, LINQ query syntax, deconstruction, Source Link, and multi-targeting** — making error-aware programming feel like a first-class citizen in modern .NET.

## ✨ Why Outcome?

ErrorOr pioneered the idea of replacing exceptions with a discriminated union of either a value or errors.  
Outcome takes this idea further:

- **Structured errors**: Rich `Error` record with `Code`, `Description`, and `Severity`.
- **Async composition**: `BindAsync`, `MapAsync`, `CombineAsync` for natural async pipelines.
- **LINQ integration**: Native `Select`/`SelectMany` support for sync + async queries.
- **Deconstruction**: Tuple-style unpacking `(isSuccess, value, errors)` for ergonomic handling.
- **Friendly ToString**: Human-readable logging like `Success: 42` or `Errors: [DIV_ZERO: Division by zero]`.
- **Multi-targeting**: Works across `netstandard2.0`, `net6.0`, and `net8.0`.
- **Source Link enabled**: Step directly into source when debugging NuGet packages.
- **Source generator support**: Auto-generate `Error<T>` helper properties from enums with the `[QbqOutcome]` attribute.
- **CQRS support**: Built-in `IMediator`, `ICommand`, and pipeline behavior infrastructure for command-query responsibility segregation.

## 🚀 Example

```csharp
var query =
    from x in ParseAsync("10")
    from y in DivideAsync(x, 2)
    select y * 2;

var (ok, value, errors) = await query;

Console.WriteLine(ok
    ? $"Result: {value}"
    : $"Errors: {string.Join("; ", errors.Select(e => e.Description))}");
```
Output:
> Result: 10

## 📦 Installation
```
dotnet add package BbQ.Outcome
dotnet add package BbQ.Cqrs
```

## 🔧 Source Generator: Error Helper Properties

The `[QbqOutcome]` attribute enables automatic generation of `Error<TCode>` helper properties for enums. This eliminates boilerplate and keeps error definitions DRY.

### Usage

Mark your error enum with `[QbqOutcome]`:

```csharp
[QbqOutcome]
public enum ApiErrorCode
{
    /// <summary>
    /// The requested resource was not found.
    /// </summary>
    NotFound,

    /// <summary>
    /// The user does not have permission to access this resource.
    /// </summary>
    Unauthorized,

    /// <summary>
    /// An internal server error occurred.
    /// </summary>
    InternalError
}
```

The source generator automatically creates a static class `ApiErrorCodeErrors` with helper properties:

```csharp
// Generated code (do not edit)
public static class ApiErrorCodeErrors
{
    public static Error<ApiErrorCode> NotFoundError =>
        new Error<ApiErrorCode>(
            Code: ApiErrorCode.NotFound,
            Description: "The requested resource was not found.",
            Severity: ErrorSeverity.Error
        );

    public static Error<ApiErrorCode> UnauthorizedError =>
        new Error<ApiErrorCode>(
            Code: ApiErrorCode.Unauthorized,
            Description: "The user does not have permission to access this resource.",
            Severity: ErrorSeverity.Error
        );

    public static Error<ApiErrorCode> InternalErrorError =>
        new Error<ApiErrorCode>(
            Code: ApiErrorCode.InternalError,
            Description: "An internal server error occurred.",
            Severity: ErrorSeverity.Error
        );
}
```

### Custom Descriptions

You can specify error descriptions in two ways:

#### 1. Using XML Documentation (Recommended)
```csharp
[QbqOutcome]
public enum ApiErrorCode
{
    /// <summary>
    /// The requested resource was not found.
    /// </summary>
    NotFound
}
```

#### 2. Using [Description] Attribute
```csharp
[QbqOutcome]
public enum ApiErrorCode
{
    [System.ComponentModel.Description("Resource not found")]
    NotFound
}
```

The generator prioritizes `[Description]` attributes over XML comments. If neither is provided, it uses the enum member name as a fallback.

### Custom Severity Levels

By default, all generated errors use `ErrorSeverity.Error`. You can customize the severity for individual enum members using the `[ErrorSeverity(...)]` attribute:

```csharp
[QbqOutcome]
public enum ApiErrorCode
{
    /// <summary>
    /// Validation failed.
    /// </summary>
    [ErrorSeverity(ErrorSeverity.Validation)]
    ValidationFailed,

    /// <summary>
    /// The requested resource was not found.
    /// </summary>
    NotFound,  // Uses default ErrorSeverity.Error

    /// <summary>
    /// An internal server error occurred.
    /// </summary>
    [ErrorSeverity(ErrorSeverity.Critical)]
    InternalError
}
```

Generated code respects the specified severity levels:

```csharp
// Generated code (do not edit)
public static class ApiErrorCodeErrors
{
    public static Error<ApiErrorCode> ValidationFailedError =>
        new Error<ApiErrorCode>(
            Code: ApiErrorCode.ValidationFailed,
            Description: "Validation failed.",
            Severity: ErrorSeverity.Validation  // Custom severity
        );

    public static Error<ApiErrorCode> NotFoundError =>
        new Error<ApiErrorCode>(
            Code: ApiErrorCode.NotFound,
            Description: "The requested resource was not found.",
            Severity: ErrorSeverity.Error  // Default severity
        );

    public static Error<ApiErrorCode> InternalErrorError =>
        new Error<ApiErrorCode>(
            Code: ApiErrorCode.InternalError,
            Description: "An internal server error occurred.",
            Severity: ErrorSeverity.Critical  // Custom severity
        );
}
```

### Available Severity Levels

The `ErrorSeverity` enum provides the following levels:

- **`Info`**: Informational message; does not indicate a failure.
- **`Validation`**: Validation failure; the operation did not meet required conditions.
- **`Warning`**: Warning; the operation may have succeeded but with unexpected side effects.
- **`Error`**: Standard error; the operation failed and the error should be handled. **(default)**
- **`Critical`**: Critical error; the system may be in an inconsistent state.

### Benefits

- **Zero boilerplate**: No manual `Error<TCode>` construction.
- **Documentation-driven**: Descriptions are extracted from XML doc comments (`<summary>` tags) or `[Description]` attributes.
- **Flexibility**: Choose between explicit `[Description]` attributes or self-documenting XML comments.
- **Severity control**: Customize error severity per enum member with `[ErrorSeverity(...)]`.
- **Consistent naming**: Property names follow the pattern `{EnumMember}Error`.
- **Type-safe**: Full compile-time type checking with `Error<YourEnumType>`.

### How It Works

1. The generator scans for enums decorated with `[QbqOutcome]`.
2. For each enum member, it extracts:
   - **Description** (in priority order):
     1. `[Description("...")]` attribute if present
     2. `<summary>` from XML documentation comments
     3. Enum member name as fallback
   - **Severity** from `[ErrorSeverity(...)]` attribute (defaults to `ErrorSeverity.Error`)
3. It generates a static helper class with pre-constructed `Error<T>` properties using named parameters.
4. All special characters in descriptions are properly escaped for C# string literals.

---

## 🎯 CQRS (Command Query Responsibility Segregation)

BbQ.Cqrs provides a lightweight, extensible CQRS implementation that integrates seamlessly with `Outcome` for error handling.

### Core Components

- **`IMediator`**: The central dispatcher for commands and queries
- **`ICommand<TResult>`**: Contract for command handlers that produce a result
- **`IPipelineBehavior<TRequest, TResponse>`**: Extensible middleware for cross-cutting concerns

### Example: Using CQRS with Outcome

```csharp
// Define a command with an Outcome result
public class CreateUserCommand : ICommand<Outcome<User>>
{
    public string Email { get; set; }
    public string Name { get; set; }
}

// Define an error enum for the domain
[QbqOutcome]
public enum UserErrorCode
{
    [Description("Email is already in use")]
    [ErrorSeverity(ErrorSeverity.Validation)]
    EmailAlreadyExists,

    [Description("Invalid email format")]
    [ErrorSeverity(ErrorSeverity.Validation)]
    InvalidEmail
}

// Implement the handler
public class CreateUserCommandHandler : ICommand<CreateUserCommand, Outcome<User>>
{
    private readonly IUserRepository _repository;
    private readonly IValidator<CreateUserCommand> _validator;

    public async Task<Outcome<User>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        // Validation using pipeline behavior
        var user = new User { Email = request.Email, Name = request.Name };
        
        if (await _repository.ExistsByEmailAsync(user.Email, cancellationToken))
        {
            return UserErrorCodeErrors.EmailAlreadyExistsError.ToOutcome<User>();
        }

        await _repository.AddAsync(user, cancellationToken);
        return Outcome<User>.From(user);
    }
}

// Use the mediator
var command = new CreateUserCommand { Email = "user@example.com", Name = "John Doe" };
var outcome = await mediator.Send(command);

var (success, user, errors) = outcome;
if (success)
{
    Console.WriteLine($"User created: {user.Name}");
}
else
{
    Console.WriteLine($"Failed: {string.Join("; ", errors.Select(e => e.Description))}");
}
```

### Pipeline Behaviors

Extend command processing with custom behaviors:

```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public async Task<TResponse> Handle(TRequest request, Func<Task<TResponse>> next)
    {
        _logger.LogInformation("Processing {RequestType}", typeof(TRequest).Name);
        var result = await next();
        _logger.LogInformation("Completed {RequestType}", typeof(TRequest).Name);
        return result;
    }
}

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IValidatable
{
    private readonly IValidator<TRequest> _validator;

    public async Task<TResponse> Handle(TRequest request, Func<Task<TResponse>> next)
    {
        var validationResult = await _validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            // Return validation errors as Outcome
            var errors = validationResult.Errors
                .Select(e => new { e.PropertyName, e.ErrorMessage })
                .ToList();
            // Handle as appropriate for your application
        }
        return await next();
    }
}
```

### Dependency Injection

Register CQRS in your service collection:

```csharp
services.AddCqrs();
services.AddPipelineBehavior<LoggingBehavior<,>>();
services.AddPipelineBehavior<ValidationBehavior<,>>();
```

### Benefits

- **Separation of Concerns**: Clear distinction between commands (write operations) and queries (read operations)
- **Composability**: Chain multiple behaviors for logging, validation, caching, etc.
- **Type Safety**: Strongly-typed request/response handling
- **Testability**: Easy to mock and test individual handlers and behaviors
- **Error Handling**: Seamless integration with `Outcome` for comprehensive error management

---
