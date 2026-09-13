using BbQ.Outcome;

namespace BbQ.Cqrs.Validation;

/// <summary>A validation failure independent of a response or validation framework.</summary>
public sealed record ValidationIssue(string Code, string Message, string? MemberName = null);

/// <summary>Validates a request asynchronously. An empty list means the request is valid.</summary>
public interface IRequestValidator<in TRequest>
{
    /// <summary>Returns ordered validation issues; does not throw for normal invalid input.</summary>
    Task<IReadOnlyList<ValidationIssue>> ValidateAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>Constructs the application's response type without payload casts or reflection.</summary>
public interface IValidationFailureFactory<out TResponse>
{
    /// <summary>Creates a failure from a nonempty list of issues.</summary>
    TResponse Create(IReadOnlyList<ValidationIssue> issues);
}

/// <summary>Maps one validation issue to the application's error model.</summary>
public interface IValidationIssueMapper<out TError>
{
    /// <summary>Maps an issue; the returned error must be non-null.</summary>
    TError Map(ValidationIssue issue);
}

/// <summary>Adapts a mapping function without changing the validation behavior.</summary>
public sealed class DelegateValidationIssueMapper<TError> : IValidationIssueMapper<TError>
{
    private readonly Func<ValidationIssue, TError> _mapper;
    public DelegateValidationIssueMapper(Func<ValidationIssue, TError> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        _mapper = mapper;
    }
    public TError Map(ValidationIssue issue) => _mapper(issue);
}

/// <summary>Preserves the standard validation issue as the typed domain error.</summary>
public sealed class IdentityValidationIssueMapper : IValidationIssueMapper<ValidationIssue>
{
    public ValidationIssue Map(ValidationIssue issue) => issue;
}

/// <summary>Projects validation issues for HTTP, logging, or localization adapters.</summary>
public sealed class ValidationIssueDescriptorProvider : IErrorDescriptorProvider<ValidationIssue>
{
    public ErrorDescriptor Describe(ValidationIssue issue)
    {
        ValidationIssues.Validate(issue);
        return new(issue.Code, issue.Message, ErrorSeverity.Validation, issue.MemberName);
    }
}

internal static class ValidationIssues
{
    internal static void Validate(ValidationIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        if (string.IsNullOrWhiteSpace(issue.Code))
            throw new ArgumentException("A validation issue requires a nonblank code.", nameof(issue));
        ArgumentNullException.ThrowIfNull(issue.Message);
    }

    internal static void ValidateFailure(IReadOnlyList<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        if (issues.Count == 0)
            throw new ArgumentException("A validation failure requires at least one issue.", nameof(issues));
        foreach (var issue in issues)
            Validate(issue);
    }
}
