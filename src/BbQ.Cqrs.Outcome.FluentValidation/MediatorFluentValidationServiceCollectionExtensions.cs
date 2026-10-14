using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using BbQ.Cqrs;
using BbQ.Cqrs.Validation;
using BbQ.Cqrs.Validation.FluentValidation;
using BbQ.Outcome;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Composes Outcome validation with scoped FluentValidation discovery and asynchronous adapters.</summary>
public static class MediatorFluentValidationServiceCollectionExtensions
{
    /// <summary>
    /// Registers mediator services, handlers, native and FluentValidation validators, and Outcome validation.
    /// FluentValidation failures become failed Outcomes through one asynchronous adapter per request type.
    /// </summary>
    /// <remarks>
    /// Concrete closed types in the explicitly supplied assemblies are discovered. Existing factories and
    /// identical registrations are preserved. Use the generated counterpart for registration without scanning.
    /// </remarks>
    /// <example><code>
    /// services.AddBbQMediatorWithOutcomeAndFluentValidationFromAssemblies(
    ///     assemblies: [typeof(CreateUser).Assembly],
    ///     configureValidation: validation => validation.MapIssuesTo&lt;AppError&gt;(MapIssue));
    /// </code></example>
    [RequiresUnreferencedCode("Assembly scanning requires preserved validators. Use generated registration for trimming.")]
    [RequiresDynamicCode("Assembly scanning constructs closed adapters. Use generated registration for Native AOT.")]
    public static IServiceCollection AddBbQMediatorWithOutcomeAndFluentValidationFromAssemblies(
        this IServiceCollection services, Assembly[] assemblies,
        Action<OutcomeValidationOptions>? configureValidation = null)
    {
        services.AddBbQMediatorWithOutcomeValidationFromAssemblies(assemblies, configureValidation);
        var requests = new HashSet<Type>();
        foreach (var type in assemblies.Distinct().SelectMany(assembly => assembly.GetTypes())
            .Where(type => !type.IsAbstract && !type.IsInterface && !type.ContainsGenericParameters)
            .OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            foreach (var contract in type.GetInterfaces().Where(contract => contract.IsGenericType))
            {
                var definition = contract.GetGenericTypeDefinition();
                if (type.IsClass && definition == typeof(IValidator<>))
                {
                    services.TryAddEnumerable(ServiceDescriptor.Scoped(contract, type));
                    requests.Add(contract.GenericTypeArguments[0]);
                }
                if (definition != typeof(IRequest<>)) continue;
                var response = contract.GenericTypeArguments[0];
                if (response.IsGenericType && (response.GetGenericTypeDefinition() == typeof(Outcome<>)
                    || response.GetGenericTypeDefinition() == typeof(Outcome<,>)))
                    requests.Add(type);
            }
        }
        foreach (var request in requests.OrderBy(type => type.FullName, StringComparer.Ordinal))
            services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IRequestValidator<>).MakeGenericType(request),
                typeof(FluentValidationRequestValidator<>).MakeGenericType(request)));
        return services;
    }
}
