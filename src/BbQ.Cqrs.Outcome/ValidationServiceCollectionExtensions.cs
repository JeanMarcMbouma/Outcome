using BbQ.Cqrs.Validation;
using BbQ.Outcome;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Explicit closed registrations; no runtime assembly scanning is required.</summary>
public static class OutcomeValidationServiceCollectionExtensions
{
    /// <summary>Registers a heterogeneous outcome factory and one request validation behavior.</summary>
    public static IServiceCollection AddOutcomeValidation<TRequest, T>(this IServiceCollection services)
        where TRequest : BbQ.Cqrs.IRequest<Outcome<T>>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IValidationFailureFactory<Outcome<T>>, OutcomeValidationFailureFactory<T>>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<BbQ.Cqrs.IPipelineBehavior<TRequest, Outcome<T>>,
            ValidationBehavior<TRequest, Outcome<T>>>());
        return services;
    }

    /// <summary>Registers a typed outcome factory and request behavior. Register an issue mapper for TError.</summary>
    public static IServiceCollection AddOutcomeValidation<TRequest, T, TError>(this IServiceCollection services)
        where TRequest : BbQ.Cqrs.IRequest<Outcome<T, TError>>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IValidationFailureFactory<Outcome<T, TError>>, OutcomeValidationFailureFactory<T, TError>>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<BbQ.Cqrs.IPipelineBehavior<TRequest, Outcome<T, TError>>,
            ValidationBehavior<TRequest, Outcome<T, TError>>>());
        return services;
    }

    /// <summary>Registers a thread-safe mapping function. Use a scoped service for stateful mappers.</summary>
    public static IServiceCollection AddValidationIssueMapper<TError>(this IServiceCollection services,
        Func<ValidationIssue, TError> mapper)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(mapper);
        services.AddSingleton<IValidationIssueMapper<TError>>(new DelegateValidationIssueMapper<TError>(mapper));
        return services;
    }
}
