namespace BbQ.Cqrs.Validation;

/// <summary>
/// Validates requests with sequential async validators, accumulating all issues in registration
/// order. The response factory owns error translation; this behavior never casts a response.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IRequestValidator<TRequest>[] _validators;
    private readonly IValidationFailureFactory<TResponse> _factory;

    public ValidationBehavior(IEnumerable<IRequestValidator<TRequest>> validators,
        IValidationFailureFactory<TResponse> factory)
    {
        ArgumentNullException.ThrowIfNull(validators);
        ArgumentNullException.ThrowIfNull(factory);
        _validators = validators.ToArray();
        if (_validators.Any(validator => validator is null))
            throw new ArgumentException("Validators cannot contain null entries.", nameof(validators));
        _factory = factory;
    }

    public async Task<TResponse> Handle(TRequest request, CancellationToken ct,
        Func<TRequest, CancellationToken, Task<TResponse>> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        ct.ThrowIfCancellationRequested();
        List<ValidationIssue>? issues = null;
        // Sequential execution supports validators sharing scoped, non-thread-safe dependencies.
        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(request, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException("A request validator returned a null issue list.");
            ct.ThrowIfCancellationRequested();
            foreach (var issue in result)
            {
                ValidationIssues.Validate(issue);
                (issues ??= []).Add(issue);
            }
        }
        return issues is null ? await next(request, ct).ConfigureAwait(false)
            : _factory.Create(issues.AsReadOnly());
    }
}
