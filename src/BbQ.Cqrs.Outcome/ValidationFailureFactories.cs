using BbQ.Outcome;

namespace BbQ.Cqrs.Validation;

/// <summary>Creates heterogeneous failures, preserving ValidationIssue objects by default.</summary>
public sealed class OutcomeValidationFailureFactory<T> : IValidationFailureFactory<Outcome<T>>
{
    private readonly IValidationIssueMapper<object?>? _mapper;

    /// <summary>An optional mapper can translate issues to an application-specific heterogeneous error.</summary>
    public OutcomeValidationFailureFactory(IValidationIssueMapper<object?>? mapper = null) => _mapper = mapper;

    public Outcome<T> Create(IReadOnlyList<ValidationIssue> issues)
    {
        ValidationIssues.ValidateFailure(issues);
        var errors = new object?[issues.Count];
        for (var i = 0; i < errors.Length; i++)
            errors[i] = _mapper is null ? issues[i] : _mapper.Map(issues[i]);
        return Outcome<T>.FromErrors(errors);
    }
}

/// <summary>Creates typed failures through an explicitly supplied issue mapper.</summary>
public sealed class OutcomeValidationFailureFactory<T, TError> : IValidationFailureFactory<Outcome<T, TError>>
{
    private readonly IValidationIssueMapper<TError> _mapper;

    public OutcomeValidationFailureFactory(IValidationIssueMapper<TError> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        _mapper = mapper;
    }

    public Outcome<T, TError> Create(IReadOnlyList<ValidationIssue> issues)
    {
        ValidationIssues.ValidateFailure(issues);
        var errors = new TError[issues.Count];
        for (var i = 0; i < errors.Length; i++)
            errors[i] = _mapper.Map(issues[i]);
        return Outcome<T, TError>.FromErrors(errors);
    }
}
