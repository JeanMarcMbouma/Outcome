using BbQ.Cqrs;
using BbQ.Cqrs.Testing;
using BbQ.Outcome;
using System.Diagnostics;

namespace BbQ.CQRS.Samples;

/// <summary>
/// Sample applications demonstrating CQRS patterns with Outcome error handling.
/// 
/// This file demonstrates:
/// 1. BasicQuery - Simple read-only query handler
/// 2. CommandWithValidation - Command with validation behavior
/// 3. ErrorHandling - Strongly-typed error access and patterns
/// 4. AdvancedBehaviors - Retry logic for transient errors
/// 
/// Each scenario is self-contained and can be run independently.
/// </summary>
static class Program
{
    static async Task Main()
    {
        Console.WriteLine("=== BbQ.Cqrs Sample Scenarios ===\n");

        // Run each scenario
        await Scenario01_BasicQuery();
        await Scenario02_CommandWithValidation();
        await Scenario03_ErrorHandling();
        await Scenario04_RetryBehavior();
    }

    /// <summary>
    /// Scenario 1: Basic Query Handler
    /// 
    /// Demonstrates:
    /// - Simple query definition
    /// - Query handler implementation
    /// - Using TestMediator for unit testing
    /// - Pattern matching with Switch()
    /// </summary>
    static async Task Scenario01_BasicQuery()
    {
        Console.WriteLine("--- Scenario 1: Basic Query Handler ---");

        // Setup
        var repository = new FakeUserRepository();
        var handler = new GetUserByIdHandler(repository);
        var mediator = new TestMediator<GetUserById, Outcome<UserDto>>(handler, []);

        // Execute
        var query = new GetUserById("123");
        var outcome = await mediator.Send(query);

        // Process result
        outcome.Switch(
            onSuccess: user => Console.WriteLine($"✓ Found user: {user.Name}"),
            onError: errors => Console.WriteLine($"✗ Error: {string.Join(", ", errors.OfType<Error<AppError>>().Select(e => e.Description))}")
        );

        Console.WriteLine();
    }

    /// <summary>
    /// Scenario 2: Command with Validation Behavior
    /// 
    /// Demonstrates:
    /// - Command definition
    /// - Command handler implementation
    /// - Validation behavior in the pipeline
    /// - Error handling for validation failures
    /// </summary>
    static async Task Scenario02_CommandWithValidation()
    {
        Console.WriteLine("--- Scenario 2: Command with Validation ---");

        // Setup
        var repository = new FakeUserRepository();
        var handler = new RenameUserHandler(repository);
        var validator = new RenameUserValidator();
        var validationBehavior = new ValidationBehavior<RenameUser, Outcome<Unit>, Unit>(validator);
        var mediator = new TestMediator<RenameUser, Outcome<Unit>>(handler, [validationBehavior]);

        // Test 1: Invalid input (empty name)
        Console.WriteLine("Test 1: Invalid input (empty name)");
        var badCommand = new RenameUser("123", "");
        var badResult = await mediator.Send(badCommand);
        badResult.Switch(
            onSuccess: _ => Console.WriteLine("✓ Unexpected success"),
            onError: errors => Console.WriteLine($"✗ Expected validation error: {errors.OfType<Error<AppError>>().FirstOrDefault()?.Description ?? "Unknown"}")
        );

        // Test 2: Valid input
        Console.WriteLine("\nTest 2: Valid input");
        var goodCommand = new RenameUser("123", "Alice");
        var goodResult = await mediator.Send(goodCommand);
        goodResult.Switch(
            onSuccess: _ => Console.WriteLine("✓ Successfully renamed user"),
            onError: errors => Console.WriteLine($"✗ Unexpected error: {errors.OfType<Error<AppError>>().FirstOrDefault()?.Description ?? "Unknown"}")
        );

        Console.WriteLine();
    }

    /// <summary>
    /// Scenario 3: Strongly-Typed Error Handling
    /// 
    /// Demonstrates:
    /// - Defining typed error enums with [QbqOutcome]
    /// - Creating strongly-typed Error<T> instances
    /// - Filtering errors by code type
    /// - Inspecting error properties (Code, Description, Severity)
    /// - Pattern matching with type-safe errors
    /// </summary>
    static async Task Scenario03_ErrorHandling()
    {
        Console.WriteLine("--- Scenario 3: Strongly-Typed Error Handling ---");

        // Simulate an outcome with multiple errors
        var outcome = Outcome<string>.FromErrors([
            new Error<AppError>(AppError.UserNotFound, "User 123 not found", ErrorSeverity.Error),
            new Error<AppError>(AppError.InvalidName, "Name contains invalid characters", ErrorSeverity.Validation),
            new Error<string>("UNTYPED_ERROR", "Some other error", ErrorSeverity.Error)
        ]);

        // Extract and display strongly-typed errors
        Console.WriteLine("All AppError instances:");
        var appErrors = outcome.Errors.OfType<Error<AppError>>().ToList();
        foreach (var error in appErrors)
        {
            Console.WriteLine($"  - [{error.Severity}] {error.Code}: {error.Description}");
        }

        // Get first error of AppError type
        var firstAppError = outcome.Errors.OfType<Error<AppError>>().FirstOrDefault();
        Console.WriteLine($"\nFirst AppError: {firstAppError?.Code}");

        // Check if errors exist
        if (outcome.Errors.OfType<Error<AppError>>().Any())
        {
            Console.WriteLine("✓ Outcome contains AppError instances");
        }

        // Filter by severity
        var validationErrors = outcome.Errors
            .OfType<Error<AppError>>()
            .Where(e => e.Severity == ErrorSeverity.Validation)
            .ToList();
        Console.WriteLine($"\nValidation errors: {string.Join(", ", validationErrors.Select(e => e.Description))}");

        Console.WriteLine();

        // Suppress warning for unused outcome
        _ = await Task.FromResult(outcome);
    }

    /// <summary>
    /// Scenario 4: Advanced Behaviors - Retry
    /// 
    /// Demonstrates:
    /// - Custom pipeline behavior implementation
    /// - Handling transient errors
    /// - Retry logic with configurable delays
    /// - Testing behaviors in isolation
    /// </summary>
    static async Task Scenario04_RetryBehavior()
    {
        Console.WriteLine("--- Scenario 4: Retry Behavior for Transient Errors ---");

        // Setup handler that always fails with transient error
        var handler = new BbQ.Cqrs.Testing.StubHandler<GetUserById, Outcome<UserDto>>(
            async (req, ct) =>
            {
                await Task.Yield();
                return Outcome<UserDto>.FromError(
                    new Error<AppError>(
                        AppError.Transient,
                        "Transient error - will be retried",
                        ErrorSeverity.Error
                    )
                );
            }
        );

        // Create retry behavior (max 3 attempts, 100ms delay)
        var retryBehavior = new RetryBehavior<GetUserById, Outcome<UserDto>, UserDto>(
            maxAttempts: 3,
            delay: TimeSpan.FromMilliseconds(100)
        );
        var mediator = new TestMediator<GetUserById, Outcome<UserDto>>(handler, [retryBehavior]);

        // Execute query
        Console.WriteLine("Sending query with retry behavior (3 max attempts)...");
        var timer = Stopwatch.StartNew();
        var outcome = await mediator.Send(new GetUserById("42"));
        timer.Stop();

        // Check result
        outcome.Switch(
            onSuccess: _ => Console.WriteLine("✓ Success after retry"),
            onError: errors =>
            {
                var error = errors.OfType<Error<AppError>>().FirstOrDefault();
                Console.WriteLine($"✗ Failed after retries: {error?.Description}");
                Console.WriteLine($"  Total time: {timer.ElapsedMilliseconds}ms (expected ~200ms for 2 retries with 100ms delay)");
            }
        );

        Console.WriteLine();
    }

    /// <summary>
    /// Fake repository for testing
    /// </summary>
    class FakeUserRepository : IUserRepository
    {
        public Task<(bool Found, string Id, string Name)> FindAsync(string id, CancellationToken ct)
        {
            return Task.FromResult((true, id, "John Doe"));
        }

        public Task SaveAsync((string Id, string Name) user, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
