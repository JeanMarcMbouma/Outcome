using BbQ.Cqrs.Validation;

namespace BbQ.CQRS.Samples;

// Response construction is provided by the optional BbQ.Cqrs.Outcome package.
// No sample-specific behavior or cast from Outcome<T> to an arbitrary TResponse is needed.
public sealed class RenameUserValidator : IRequestValidator<RenameUser>
{
    public Task<IReadOnlyList<ValidationIssue>> ValidateAsync(RenameUser request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<ValidationIssue> issues = string.IsNullOrWhiteSpace(request.NewName)
            ? new[] { new ValidationIssue("INVALID_NAME", "New name must be non-empty", nameof(request.NewName)) }
            : request.NewName.Length > 50
                ? new[] { new ValidationIssue("INVALID_NAME", "New name must be at most 50 characters", nameof(request.NewName)) }
                : Array.Empty<ValidationIssue>();
        return Task.FromResult(issues);
    }
}
