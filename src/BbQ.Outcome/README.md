# BbQ.Outcome

A modern C# functional result type inspired by [ErrorOr](https://github.com/amantinband/error-or).  
It builds on the excellent foundation of ErrorOr by adding **async workflows, LINQ query syntax, deconstruction, Source Link, and multi-targeting** — making error-aware programming feel like a first-class citizen in modern .NET.

## ❓ Why BbQ.Outcome?

ErrorOr pioneered the idea of replacing exceptions with a discriminated union of either a value or errors.  
BbQ.Outcome takes this idea further:

- **Structured errors**: Rich `Error` record with `Code`, `Description`, and `Severity`.
- **Async composition**: `BindAsync`, `MapAsync`, `CombineAsync` for natural async pipelines.
- **LINQ integration**: Native `Select`/`SelectMany` support for sync + async queries.
- **IAsyncEnumerable streaming**: `Select`, `Bind`, `Map`, `Where`, `Values`, `Errors` over `IAsyncEnumerable<Outcome<T>>` streams.
- **Deconstruction**: Tuple-style unpacking `(isSuccess, value, errors)` for ergonomic handling.
- **Friendly ToString**: Human-readable logging like `Success: 42` or `Errors: [DIV_ZERO: Division by zero]`.
- **Multi-targeting**: Works across `net8.0`, `net9.0`, and `net10.0`.
- **Source Link enabled**: Step directly into source when debugging NuGet packages.
- **Source generator support**: Auto-generate `Error<T>` helper properties from enums with the `[QbqOutcome]` attribute.

## 💡 Example

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

## 💾 Installation
```
dotnet add package BbQ.Outcome
```

## 🧩 Source Generator: Error Helper Properties

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

- **Zero boilerplate**: No manual `Error<T>` construction.
- **Documentation-driven**: Descriptions are extracted from XML doc comments (`<summary>` tags) or `[Description]` attributes.
- **Flexibility**: Choose between explicit `[Description]` attributes or self-documenting XML comments.
- **Severity control**: Customize error severity per enum member with `[ErrorSeverity(...)]`.

## ⚡ Performance

Both `Outcome<T>` (untyped errors) and `Outcome<T, TError>` (typed errors) are zero-allocation `readonly struct` types. Hot-path operations use internal unchecked accessors and `[AggressiveInlining]` to ensure the JIT can fully inline composition chains.

`Outcome<T>` wraps `Outcome<T, object?>` internally. Despite this extra layer of indirection, Map, Bind, Match, and Pipeline operations perform at **near-identical speed** to the direct `Outcome<T, TError>` type across .NET 8, 9, and 10:

| Operation | .NET 8 (ns) | .NET 9 (ns) | .NET 10 (ns) | Allocations |
|---|---:|---:|---:|---|
| Create (success) | ~1.7 | ~2.0 | ~1.6 | 0 B |
| Map (success) | ~1.9 | ~1.7 | ~2.0 | 0 B |
| Bind (success) | ~2.0–7.2 | ~2.2 | ~2.3 | 0 B |
| Match (success) | <1 | <1 | <1 | 0 B |
| Pipeline (Map+Bind chain) | ~16 | ~15 | ~13 | 32 B |

Full benchmark results: [tests/BbQ.Outcome.Benchmarks/README.md](../../tests/BbQ.Outcome.Benchmarks/README.md)
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

## 🔁 Common Patterns

### Async Pipelines with Bind

```csharp
public async Task<Outcome<User>> GetAndValidateUserAsync(Guid userId)
{
    return await GetUserAsync(userId)
        .BindAsync(user => ValidateUserAsync(user))
        .BindAsync(user => EnrichUserAsync(user));
}
```

### Pattern Matching with Match

```csharp
var outcome = await CreateUserAsync(email, name);

string message = outcome.Match(
    onSuccess: user => $"Created user {user.Name}",
    onError: errors => $"Failed: {string.Join(", ", errors.Select(e => e.Description))}"
);
```

### LINQ Queries

```csharp
var results = from user in GetUsersAsync()
              from validated in ValidateAsync(user)
              select validated;

var (ok, users, errors) = await results;
```

### Deconstruction

```csharp
var (success, value, errors) = outcome;

if (success)
{
    Console.WriteLine($"Success: {value}");
}
else
{
    foreach (var error in errors)
    {
        Console.WriteLine($"[{error.Severity}] {error.Code}: {error.Description}");
    }
}
```

### IAsyncEnumerable Streaming

Process async streams of outcomes with railway-oriented composition — useful when consuming `IAsyncEnumerable<Outcome<T>>` from CQRS streaming queries or event subscriptions:

```csharp
IAsyncEnumerable<Outcome<Order>> orderStream = mediator.Stream(query, ct);

// Transform, filter, and extract values from the stream
await foreach (var name in orderStream
    .Where(o => o.Total > 100)
    .Map(o => o.CustomerName)
    .Values())
{
    Console.WriteLine(name);
}
```

#### Available Stream Operations

| Method | Signature | Description |
|--------|-----------|-------------|
| `Select` | `IAsyncEnumerable<Outcome<TResult>>` | LINQ-style map over success values; errors propagate |
| `Map` | `IAsyncEnumerable<Outcome<TResult>>` | Functor map (alias for Select, for API consistency) |
| `Bind` | `IAsyncEnumerable<Outcome<TResult>>` | Monadic flatMap producing new outcomes |
| `SelectMany` | `IAsyncEnumerable<Outcome<TResult>>` | LINQ flatMap with projection |
| `Where` | `IAsyncEnumerable<Outcome<T>>` | Predicate filter (failures become validation errors) |
| `Values` | `IAsyncEnumerable<T>` | Extract only success values, discard errors |
| `Errors` | `IAsyncEnumerable<IReadOnlyList<object?>>` | Extract only error lists, discard successes |

All methods support `CancellationToken` and use `ConfigureAwait(false)`.

#### Bind over a Stream

```csharp
// Validate each item in the stream; invalid items become error outcomes
var validated = orderStream.Bind(order =>
    order.Total > 0
        ? Outcome<Order>.From(order)
        : Outcome<Order>.Validation("INVALID_TOTAL", "Order total must be positive"));

await foreach (var outcome in validated)
{
    outcome.Switch(
        onSuccess: order => Console.WriteLine($"Valid order: {order.Id}"),
        onError: errors => Console.WriteLine($"Invalid: {errors[0]}")
    );
}
```

#### Collecting Errors from a Stream

```csharp
// Log only errors from the stream
await foreach (var errorList in orderStream.Errors())
{
    foreach (var error in errorList)
    {
        logger.LogWarning("Stream error: {Error}", error);
    }
}
```

---

## 🔗 Integration with CQRS

When using **BbQ.Cqrs**, combine Outcome with commands and queries for comprehensive error handling:

```csharp
// Error codes
[QbqOutcome]
public enum UserErrors
{
    [Description("Email already in use")]
    [ErrorSeverity(ErrorSeverity.Validation)]
    EmailAlreadyExists
}

// Command returns Outcome<T>
public class CreateUserCommand : ICommand<Outcome<User>>
{
    public string Email { get; set; }
}

// Handler uses source-generated errors
public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Outcome<User>>
{
    public async Task<Outcome<User>> Handle(CreateUserCommand request, CancellationToken ct)
    {
        if (await UserExists(request.Email))
        {
            return UserErrorsErrors.EmailAlreadyExistsError.ToOutcome<User>();
        }
        
        return Outcome<User>.From(await CreateUser(request.Email));
    }
}
```

---

## 📚 Learn More

- [Outcome Error Helper Properties](./README.md) - Strongly typed error patterns with source generators
- [BbQ.Cqrs Documentation](../BbQ.Cqrs/README.md) - Using Outcome with CQRS
