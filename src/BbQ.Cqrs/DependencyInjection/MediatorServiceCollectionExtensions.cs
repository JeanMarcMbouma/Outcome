using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BbQ.Cqrs.DependencyInjection;

/// <summary>Registers mediator infrastructure without discovering handlers or behaviors.</summary>
public static class MediatorServiceCollectionExtensions
{
    /// <summary>Registers scoped mediator, command/query dispatchers, sender and streamer, preserving application overrides.</summary>
    /// <example><code>services.AddBbQMediatorServices();</code></example>
    public static IServiceCollection AddBbQMediatorServices(this IServiceCollection services)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        services.TryAddScoped<IMediator, Mediator>();
        services.TryAddScoped<ICommandDispatcher, CommandDispatcher>();
        services.TryAddScoped<IQueryDispatcher, QueryDispatcher>();
        services.TryAddScoped<ISender>(sp => sp.GetRequiredService<IMediator>());
        services.TryAddScoped<IStreamer>(sp => sp.GetRequiredService<IMediator>());
        return services;
    }
}
