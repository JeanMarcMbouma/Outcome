using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using BbQ.Cqrs;
using BbQ.Cqrs.DependencyInjection;
using BbQ.Cqrs.Validation;
using BbQ.Outcome;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Composes mediator registration with discovered handlers, validators and Outcome validation.</summary>
public static class MediatorOutcomeValidationServiceCollectionExtensions
{
    /// <summary>
    /// Registers scoped mediator services, handlers and validators from the supplied assemblies,
    /// and closed validation behaviors/factories for requests returning Outcome.
    /// </summary>
    /// <remarks>
    /// Only concrete closed types are discovered. Custom pipeline behaviors remain explicit.
    /// For trimming or Native AOT use the generated Add{Assembly}MediatorWithOutcomeValidation method.
    /// Supply mappings for domain error types, or a custom failure factory. ValidateOnBuild detects missing dependencies.
    /// </remarks>
    /// <example><code>
    /// services.AddBbQMediatorWithOutcomeValidationFromAssemblies(
    ///     assemblies: [typeof(CreateUser).Assembly],
    ///     configureValidation: validation => validation.MapIssuesTo&lt;string&gt;(issue => issue.Code));
    /// </code></example>
    [RequiresUnreferencedCode("Assembly scanning requires preserved request, handler and validator types. Use generated registration for trimming.")]
    [RequiresDynamicCode("Assembly scanning constructs closed validation types. Use generated registration for Native AOT.")]
    public static IServiceCollection AddBbQMediatorWithOutcomeValidationFromAssemblies(
        this IServiceCollection services, Assembly[] assemblies,
        Action<OutcomeValidationOptions>? configureValidation = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);
        if (assemblies.Length == 0 || assemblies.Any(assembly => assembly is null))
            throw new ArgumentException("Supply at least one non-null assembly to scan.", nameof(assemblies));

        services.AddBbQMediatorServices();
        configureValidation?.Invoke(new OutcomeValidationOptions(services));
        foreach (var type in assemblies.Distinct().SelectMany(assembly => assembly.GetTypes())
            .Where(type => !type.IsAbstract && !type.IsInterface && !type.ContainsGenericParameters)
            .OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            foreach (var contract in type.GetInterfaces().Where(contract => contract.IsGenericType))
            {
                var definition = contract.GetGenericTypeDefinition();
                if (type.IsClass && (definition == typeof(IRequestHandler<>) || definition == typeof(IRequestHandler<,>)
                    || definition == typeof(IStreamHandler<,>) || definition == typeof(IRequestValidator<>)))
                    services.TryAddEnumerable(ServiceDescriptor.Scoped(contract, type));

                if (definition != typeof(IRequest<>)) continue;
                var response = contract.GenericTypeArguments[0];
                if (!response.IsGenericType) continue;
                var outcome = response.GetGenericTypeDefinition();
                if (outcome != typeof(Outcome<>) && outcome != typeof(Outcome<,>)) continue;
                var arguments = response.GenericTypeArguments;
                var factory = (arguments.Length == 1 ? typeof(OutcomeValidationFailureFactory<>)
                    : typeof(OutcomeValidationFailureFactory<,>)).MakeGenericType(arguments);
                services.TryAdd(ServiceDescriptor.Scoped(typeof(IValidationFailureFactory<>).MakeGenericType(response), factory));
                if (arguments.Length == 2 && arguments[1] == typeof(ValidationIssue))
                    services.TryAddScoped<IValidationIssueMapper<ValidationIssue>, IdentityValidationIssueMapper>();
                services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IPipelineBehavior<,>).MakeGenericType(type, response),
                    typeof(ValidationBehavior<,>).MakeGenericType(type, response)));
            }
        }
        return services;
    }
}
