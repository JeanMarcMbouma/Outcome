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
        new(
            ApiErrorCode.NotFound,
            "The requested resource was not found.",
            ErrorSeverity.Error
        );

    public static Error<ApiErrorCode> UnauthorizedError =>
        new(
            ApiErrorCode.Unauthorized,
            "The user does not have permission to access this resource.",
            ErrorSeverity.Error
        );

    public static Error<ApiErrorCode> InternalErrorError =>
        new(
            ApiErrorCode.InternalError,
            "An internal server error occurred.",
            ErrorSeverity.Error
        );
}
```

### Benefits

- **Zero boilerplate**: No manual `Error<TCode>` construction.
- **Documentation-driven**: Descriptions are extracted from XML doc comments (`<summary>` tags).
- **Consistent naming**: Property names follow the pattern `{EnumMember}Error`.
- **Type-safe**: Full compile-time type checking with `Error<YourEnumType>`.

### How It Works

1. The generator scans for enums decorated with `[QbqOutcome]`.
2. For each enum member, it extracts the summary from XML documentation comments.
3. It generates a static helper class with pre-constructed `Error<T>` properties.
4. If no documentation is found, it falls back to the enum member name.

---
