# BbQ.Cqrs Sample Scenarios

This document describes the sample scenarios demonstrating CQRS patterns with Outcome error handling.

## Quick Start

Run the samples:
```bash
dotnet run --project src/BbQ.Cqrs.Samples
```

Output:
```
=== BbQ.Cqrs Sample Scenarios ===

--- Scenario 1: Basic Query Handler ---
? Found user: John Doe

--- Scenario 2: Command with Validation ---
Test 1: Invalid input (empty name)
? Expected validation error: New name must be non-empty

Test 2: Valid input
? Successfully renamed user

--- Scenario 3: Strongly-Typed Error Handling ---
All AppError instances:
  - [Error] UserNotFound: User 123 not found
  - [Validation] InvalidName: Name contains invalid characters

First AppError: UserNotFound
? Outcome contains AppError instances

Validation errors: 1

--- Scenario 4: Retry Behavior for Transient Errors ---
Sending query with retry behavior (3 max attempts)...
? Failed after retries: Transient error - will be retried
  Total time: ~200ms (expected ~200ms for 2 retries with 100ms delay)
```

---

## Scenario 1: Basic Query Handler

**Location:** `Scenario01_BasicQuery()`

**Demonstrates:**
- Simple query definition (`GetUserById`)
- Query handler implementation (`GetUserByIdHandler`)
- Using `TestMediator` for unit testing
- Pattern matching with `Switch()`
- Error handling with type casting

**Key Concepts:**
- Queries are read-only operations
- Handlers return `Outcome<T>` for error-aware results
- `TestMediator` enables isolated handler testing
- No behaviors in the pipeline (direct handler invocation)

**Example Code:**
```csharp
// Define query
public class GetUserById : IQuery<Outcome<UserDto>>
{
    public string Id { get; set; }
}

// Implement handler
public class GetUserByIdHandler : IRequestHandler<GetUserById, Outcome<UserDto>>
{
    public async Task<Outcome<UserDto>> Handle(GetUserById request, CancellationToken ct)
    {
        var (found, id, name) = await _repository.FindAsync(request.Id, ct);
        return found 
            ? Outcome<UserDto>.From(new UserDto { Id = id, Name = name })
            : Outcome<UserDto>.FromError(
                new Error<AppError>(
                    AppError.UserNotFound,
                    $"User '{request.Id}' not found"
                )
            );
    }
}

// Test with mediator
var mediator = new TestMediator<GetUserById, Outcome<UserDto>>(handler, []);
var outcome = await mediator.Send(new GetUserById("123"));

outcome.Switch(
    onSuccess: user => Console.WriteLine($"User: {user.Name}"),
    onError: errors => Console.WriteLine($"Error: {string.Join(", ", errors.OfType<Error<AppError>>().Select(e => e.Description))}")
);
```

---

## Scenario 2: Command with Validation Behavior

**Location:** `Scenario02_CommandWithValidation()`

**Demonstrates:**
- Command definition (`RenameUser`)
- Command handler implementation (`RenameUserHandler`)
- Validation behavior in the pipeline
- Error handling for validation failures
- Pipeline behaviors wrapping handlers

**Key Concepts:**
- Commands modify state (create, update, delete)
- Validation occurs in a behavior BEFORE the handler
- Behaviors can short-circuit the pipeline
- Multiple behaviors stack in execution order

**Example Code:**
```csharp
// Define command
public class RenameUser : ICommand<Outcome<Unit>>
{
    public string Id { get; set; }
    public string NewName { get; set; }
}

// Implement handler
public class RenameUserHandler : IRequestHandler<RenameUser, Outcome<Unit>>
{
    public async Task<Outcome<Unit>> Handle(RenameUser request, CancellationToken ct)
    {
        var (found, id, _) = await _repo.FindAsync(request.Id, ct);
        if (!found)
            return Outcome<Unit>.FromError(
                new Error<AppError>(AppError.UserNotFound, $"User '{request.Id}' not found")
            );
        
        var trimmed = request.NewName?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return Outcome<Unit>.FromError(
                new Error<AppError>(AppError.InvalidName, "New name must be non-empty")
            );

        await _repo.SaveAsync((id, trimmed!), ct);
        return Outcome<Unit>.From(new Unit());
    }
}

// Validation behavior
public class ValidationBehavior<TRequest, TResponse, TPayload> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IOutcome<TPayload>
{
    public async Task<TResponse> Handle(
        TRequest request,
        CancellationToken ct,
        Func<TRequest, CancellationToken, Task<TResponse>> next)
    {
        // Validate before handler
        var result = await _validator.ValidateAsync(request, ct);
        if (!result.IsValid)
            throw new ValidationException(result.Errors);
        
        // Call next behavior or handler
        return await next(request, ct);
    }
}

// Register behaviors
var mediator = new TestMediator<RenameUser, Outcome<Unit>>(
    handler, 
    [new ValidationBehavior<RenameUser, Outcome<Unit>, Unit>(validator)]
);
```

---

## Scenario 3: Strongly-Typed Error Handling

**Location:** `Scenario03_ErrorHandling()`

**Demonstrates:**
- Defining typed error enums with `[QbqOutcome]` source generator
- Creating strongly-typed `Error<T>` instances
- Filtering errors by type using LINQ
- Inspecting error properties (Code, Description, Severity)
- Pattern matching with type-safe errors

**Key Concepts:**
- Error codes are enum-based and source-generated
- `Outcome<T>` can contain multiple errors of different types
- LINQ with `OfType<T>` provides type-safe error queries
- Errors have `Severity` for different handling strategies

**Error Definition:**
```csharp
[QbqOutcome]
public enum AppError
{
    [Description("User not found")]
    UserNotFound,
    
    [Description("Invalid name")]
    [ErrorSeverity(ErrorSeverity.Validation)]
    InvalidName,
    
    [Description("Transient error")]
    Transient
}
```

**Error Access Patterns:**
```csharp
var outcome = Outcome<string>.FromErrors([
    new Error<AppError>(AppError.UserNotFound, "User 123 not found"),
    new Error<AppError>(AppError.InvalidName, "Name invalid"),
    new Error<string>("UNTYPED", "Other error")
]);

// Get all errors of a type
var appErrors = outcome.Errors.OfType<Error<AppError>>().ToList();

// Get first error of a type
var firstAppError = outcome.Errors.OfType<Error<AppError>>().FirstOrDefault();

// Check if errors exist
if (outcome.Errors.OfType<Error<AppError>>().Any())
{
    // Handle AppError instances
}

// Filter by predicate
var validationErrors = outcome.Errors
    .OfType<Error<AppError>>()
    .Where(e => e.Severity == ErrorSeverity.Validation)
    .ToList();
```

---

## Scenario 4: Advanced Behaviors - Retry

**Location:** `Scenario04_RetryBehavior()`

**Demonstrates:**
- Custom pipeline behavior implementation
- Handling transient errors
- Retry logic with configurable attempts and delay
- Testing behaviors in isolation with `TestMediator`
- Combining behaviors with handlers

**Key Concepts:**
- Retry behavior wraps the handler
- Only retries on transient errors (identified by error code)
- Configurable delays between attempts
- Behaviors execute before/after handler logic

**Retry Behavior Implementation:**
```csharp
public class RetryBehavior<TRequest, TResponse, TPayload> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IOutcome<TPayload>
{
    public async Task<TResponse> Handle(
        TRequest request,
        CancellationToken ct,
        Func<TRequest, CancellationToken, Task<TResponse>> next)
    {
        for (var attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            var outcome = await next(request, ct);
            
            if (outcome.IsSuccess)
                return outcome;
            
            // Check for transient errors
            var isTransient = outcome.Errors?
                .OfType<Error<AppError>>()
                .Any(e => e.Code == AppError.Transient) ?? false;
            
            if (!isTransient || attempt == _maxAttempts)
                return outcome;
            
            await Task.Delay(_delay, ct);
        }
        
        return await next(request, ct);
    }
}
```

**Usage:**
```csharp
var retryBehavior = new RetryBehavior<GetUserById, Outcome<UserDto>, UserDto>(
    maxAttempts: 3,
    delay: TimeSpan.FromMilliseconds(100)
);

var mediator = new TestMediator<GetUserById, Outcome<UserDto>>(
    handler,
    [retryBehavior]
);

var outcome = await mediator.Send(new GetUserById("42"));
```

---

## Common Patterns

### Pattern 1: Error Construction
```csharp
// Direct error construction
return Outcome<User>.FromError(
    new Error<AppError>(
        AppError.UserNotFound,
        "User not found",
        ErrorSeverity.Error
    )
);

// From single error
return Outcome<User>.FromError(new Error<AppError>(AppError.NotFound, "Not found"));

// Multiple errors
return Outcome<User>.FromErrors([
    new Error<AppError>(AppError.InvalidEmail, "Email invalid"),
    new Error<AppError>(AppError.InvalidName, "Name invalid")
]);
```

### Pattern 2: Result Handling
```csharp
// Pattern matching with Switch
outcome.Switch(
    onSuccess: user => Console.WriteLine($"User: {user.Name}"),
    onError: errors => Console.WriteLine($"Errors: {string.Join("; ", errors.Select(e => e.Description))}")
);

// Deconstruction
var (success, value, errors) = outcome;
if (success)
{
    Console.WriteLine($"Success: {value}");
}
else
{
    foreach (var error in errors!)
        Console.WriteLine($"Error: {error.Description}");
}
```

### Pattern 3: Chaining Operations
```csharp
// Using Bind for monadic composition
return await GetUserAsync(userId)
    .BindAsync(user => ValidateUserAsync(user))
    .BindAsync(user => UpdateUserAsync(user));

// Using Map for transformations
var nameOutcome = await GetUserAsync(userId)
    .MapAsync(user => user.Name);
```

### Pattern 4: Multiple Errors
```csharp
// Aggregate multiple validation errors
var errors = new List<object>
{
    new Error<AppError>(AppError.InvalidName, "Name is empty"),
    new Error<AppError>(AppError.InvalidEmail, "Email is invalid")
};

return Outcome<User>.FromErrors(errors);
```

---

## Testing Guide

### Test a Handler in Isolation
```csharp
[Test]
public async Task Handler_WithValidInput_ReturnsSuccess()
{
    // Arrange
    var handler = new GetUserByIdHandler(fakeRepository);
    var mediator = new TestMediator<GetUserById, Outcome<UserDto>>(handler, []);

    // Act
    var outcome = await mediator.Send(new GetUserById("123"));

    // Assert
    Assert.That(outcome.IsSuccess, Is.True);
    Assert.That(outcome.Value.Name, Is.EqualTo("John Doe"));
}
```

### Test a Behavior in Isolation
```csharp
[Test]
public async Task ValidationBehavior_WithInvalidInput_ReturnsValidationError()
{
    // Arrange
    var handler = new StubHandler<RenameUser, Outcome<Unit>>(
        async (req, ct) => throw new InvalidOperationException("Should not reach handler")
    );
    var behavior = new ValidationBehavior<RenameUser, Outcome<Unit>, Unit>(validator);
    var mediator = new TestMediator<RenameUser, Outcome<Unit>>(handler, [behavior]);

    // Act & Assert
    Assert.ThrowsAsync<ValidationException>(
        async () => await mediator.Send(new RenameUser("123", ""))
    );
}
```

### Test Multiple Behaviors
```csharp
[Test]
public async Task Pipeline_WithLoggingAndValidation_ExecutesInOrder()
{
    // Arrange
    var loggingBehavior = new LoggingBehavior<CreateUser, Outcome<User>>(logger);
    var validationBehavior = new ValidationBehavior<CreateUser, Outcome<User>, User>(validator);
    var mediator = new TestMediator<CreateUser, Outcome<User>>(
        handler,
        [loggingBehavior, validationBehavior]  // Logging first, validation second
    );

    // Act
    var outcome = await mediator.Send(new CreateUser("test@example.com", "Test"));

    // Assert
    // Verify logging was called
    logger.Verify(x => x.Log(...), Times.Once);
    // Verify validation was called
    // Verify result
}
```

---

## Project Structure

```
src/BbQ.Cqrs.Samples/
??? Program.cs                          # Main entry point with all scenarios
??? AppError.cs                         # Error enum with [QbqOutcome]
??? IUserRepository.cs                  # Repository interface
??? Unit.cs                             # Void type for commands with no return value
??? UserDto.cs                          # User data transfer object
?
??? GetUserById.cs                      # Query definition
??? GetUserByIdHandler.cs               # Query handler
?
??? RenameUser.cs                       # Command definition
??? RenameUserHandler.cs                # Command handler
??? RenameUserValidator.cs              # Fluent validator for RenameUser
?
??? ValidationBehavior.cs               # Generic validation pipeline behavior
??? LoggingBehavior.cs                  # Generic logging pipeline behavior
??? RetryBehavior.cs                    # Generic retry pipeline behavior
```

---

## Running Individual Scenarios

To run only a specific scenario, you can modify `Program.cs` Main():

```csharp
static async Task Main()
{
    Console.WriteLine("=== BbQ.Cqrs Sample Scenarios ===\n");

    // Comment out scenarios you don't want to run
    await Scenario01_BasicQuery();
    // await Scenario02_CommandWithValidation();
    // await Scenario03_ErrorHandling();
    // await Scenario04_RetryBehavior();
}
```

---

## Learning Path

1. **Start with Scenario 1** - Understand basic queries and handlers
2. **Move to Scenario 2** - Learn how behaviors work in the pipeline
3. **Explore Scenario 3** - Master strongly-typed error handling
4. **Advanced: Scenario 4** - Understand complex behavior patterns

---

## Related Documentation

- [BbQ.Outcome Documentation](../../src/Outcome/README.md)
- [BbQ.Cqrs Documentation](../../src/BbQ.Cqrs/README.md)
- [Strongly Typed Errors Guide](../../STRONGLY_TYPED_ERRORS.md)
