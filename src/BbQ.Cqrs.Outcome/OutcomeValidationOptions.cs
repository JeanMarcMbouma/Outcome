using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BbQ.Cqrs.Validation;

/// <summary>Configures validation issue mappings shared by scanning and generated registration.</summary>
public sealed class OutcomeValidationOptions
{
    private readonly IServiceCollection _services;

    /// <summary>Creates configuration for the specified service collection; used by generated registration methods.</summary>
    public OutcomeValidationOptions(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    /// <summary>Maps validation issues to domain errors. The delegate must be thread-safe.</summary>
    public OutcomeValidationOptions MapIssuesTo<TError>(Func<ValidationIssue, TError> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        _services.Replace(ServiceDescriptor.Singleton<IValidationIssueMapper<TError>>(new DelegateValidationIssueMapper<TError>(mapper)));
        return this;
    }

    /// <summary>Registers a scoped mapper implementation, which may depend on other scoped services.</summary>
    public OutcomeValidationOptions MapIssuesTo<TError, TMapper>()
        where TMapper : class, IValidationIssueMapper<TError>
    {
        _services.Replace(ServiceDescriptor.Scoped<IValidationIssueMapper<TError>, TMapper>());
        return this;
    }
}
