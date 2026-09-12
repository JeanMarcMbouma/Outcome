using FluentValidation;
using BbQ.Cqrs.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BbQ.Cqrs.Validation.FluentValidation;

/// <summary>Adapts explicitly registered FluentValidation validators using ValidateAsync only.</summary>
public sealed class FluentValidationRequestValidator<TRequest> : IRequestValidator<TRequest>
{
    private readonly IValidator<TRequest>[] _validators;

    public FluentValidationRequestValidator(IEnumerable<IValidator<TRequest>> validators)
    {
        ArgumentNullException.ThrowIfNull(validators);
        _validators = validators.ToArray();
        if (_validators.Any(validator => validator is null))
            throw new ArgumentException("Validators cannot contain null entries.", nameof(validators));
    }

    public async Task<IReadOnlyList<ValidationIssue>> ValidateAsync(TRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var issues = new List<ValidationIssue>();
        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var error in result.Errors)
                issues.Add(new(error.ErrorCode, error.ErrorMessage, error.PropertyName));
        }
        return issues.AsReadOnly();
    }
}

/// <summary>Opt-in closed adapter registration. Register IValidator instances separately.</summary>
public static class FluentValidationServiceCollectionExtensions
{
    public static IServiceCollection AddFluentValidationRequest<TRequest>(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IRequestValidator<TRequest>, FluentValidationRequestValidator<TRequest>>());
        return services;
    }
}
