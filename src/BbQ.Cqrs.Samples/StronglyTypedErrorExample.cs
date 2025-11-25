// Example demonstrating strongly-typed error access in the RenameUserHandler

using BbQ.Cqrs;
using BbQ.Outcome;

namespace BbQ.CQRS.Samples;

public sealed class RenameUserHandlerExample
    : IRequestHandler<RenameUser, Outcome<Unit>>
{
    private readonly IUserRepository _repo;
    public RenameUserHandlerExample(IUserRepository repo) => _repo = repo;

    public async Task<Outcome<Unit>> Handle(RenameUser request, CancellationToken ct)
    {
        var (found, id, name) = await _repo.FindAsync(request.Id, ct);
        if (!found)
        {
            return Outcome<Unit>.FromError(new Error<AppError>(AppError.UserNotFound, $"User '{request.Id}' not found"));
        }

        var trimmed = request.NewName?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return Outcome<Unit>.FromError(new Error<AppError>(AppError.InvalidName, "New name must be non-empty"));
        }

        await _repo.SaveAsync((id, trimmed!), ct);
        return new Unit();
    }
}

// Example demonstrating the improved error access patterns
public sealed class StronglyTypedErrorExample
{
    public void DemonstrateErrorAccess()
    {
        // Simulate an outcome with multiple errors
        var outcome = Outcome<string>.FromErrors([
            new Error<AppError>(AppError.UserNotFound, "User 123 not found"),
            new Error<AppError>(AppError.InvalidName, "Name contains invalid characters"),
            new Error<string>("UNTYPED_ERROR", "Some other error")
        ]);

        // BEFORE: Painful casting and type-checking
        // var errors = outcome.Errors; // IReadOnlyList<object?>
        // foreach (var error in errors)
        // {
        //     if (error is Error<AppError> appError)
        //     {
        //         // Handle AppError
        //     }
        // }

        // AFTER: Clean, strongly-typed access
        
        // Get all errors of a specific type
        var appErrors = outcome.GetErrors<AppError>();
        Console.WriteLine($"Found {appErrors.Count()} AppError instances");
        
        foreach (var error in appErrors)
        {
            Console.WriteLine($"  - {error.Code}: {error.Description}");
        }

        // Get the first error of a type (or null)
        var firstAppError = outcome.GetError<AppError>();
        if (firstAppError != null)
        {
            Console.WriteLine($"First AppError: {firstAppError.Code}");
        }

        // Check if errors of a type exist
        if (outcome.HasErrors<AppError>())
        {
            Console.WriteLine("This outcome contains AppError instances");
        }

        // Filter errors by predicate
        var validationErrors = outcome.GetErrors<AppError>(
            e => e.Severity == ErrorSeverity.Validation || 
                 e.Code == AppError.InvalidName
        );
        Console.WriteLine($"Found {validationErrors.Count()} validation-related errors");

        // Chain with Match for complete error handling
        var result = outcome.Match(
            onSuccess: value => $"Success: {value}",
            onError: errors =>
            {
                var appErrorList = outcome.GetErrors<AppError>();
                if (appErrorList.Any())
                {
                    return $"Application errors: {string.Join("; ", appErrorList.Select(e => e.Description))}";
                }
                return "Unknown errors occurred";
            }
        );
        Console.WriteLine(result);
    }
}
