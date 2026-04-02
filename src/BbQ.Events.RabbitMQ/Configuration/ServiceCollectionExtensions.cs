using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using BbQ.Events.Events;
using BbQ.Events.RabbitMQ.Events;

namespace BbQ.Events.RabbitMQ.Configuration;

/// <summary>
/// Extension methods for registering the RabbitMQ event bus with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the RabbitMQ event bus for distributed pub/sub messaging.
    /// </summary>
    /// <param name="services">The service collection to register with</param>
    /// <param name="connectionUri">The RabbitMQ connection URI (e.g., "amqp://guest:guest@localhost:5672/")</param>
    /// <returns>The service collection for chaining</returns>
    /// <remarks>
    /// This method registers IEventBus and IEventPublisher as singletons using RabbitMqEventBus.
    /// 
    /// Prerequisites:
    /// - RabbitMQ server must be accessible at the specified URI
    /// 
    /// Example usage:
    /// <code>
    /// services.UseRabbitMqEventBus("amqp://guest:guest@localhost:5672/");
    /// </code>
    /// </remarks>
    public static IServiceCollection UseRabbitMqEventBus(
        this IServiceCollection services,
        string connectionUri)
    {
        if (string.IsNullOrWhiteSpace(connectionUri))
        {
            throw new ArgumentNullException(nameof(connectionUri));
        }

        return services.UseRabbitMqEventBus(options =>
        {
            options.ConnectionUri = connectionUri;
        });
    }

    /// <summary>
    /// Registers the RabbitMQ event bus for distributed pub/sub messaging with custom options.
    /// </summary>
    /// <param name="services">The service collection to register with</param>
    /// <param name="configureOptions">Action to configure RabbitMQ event bus options</param>
    /// <returns>The service collection for chaining</returns>
    /// <remarks>
    /// This method registers IEventBus and IEventPublisher as singletons using RabbitMqEventBus.
    /// 
    /// Prerequisites:
    /// - RabbitMQ server must be accessible
    /// 
    /// Example usage:
    /// <code>
    /// services.UseRabbitMqEventBus(options =>
    /// {
    ///     options.HostName = "localhost";
    ///     options.Port = 5672;
    ///     options.UserName = "guest";
    ///     options.Password = "guest";
    ///     options.ExchangeName = "my-app.events";
    ///     options.DurableQueues = true;
    ///     options.PersistentMessages = true;
    /// });
    /// </code>
    /// </remarks>
    public static IServiceCollection UseRabbitMqEventBus(
        this IServiceCollection services,
        Action<RabbitMqEventBusOptions> configureOptions)
    {
        if (configureOptions == null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }

        var options = new RabbitMqEventBusOptions();
        configureOptions(options);

        // Replace any existing IEventBus registration
        services.Replace(ServiceDescriptor.Singleton<IEventBus>(sp =>
            new RabbitMqEventBus(
                sp,
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RabbitMqEventBus>>(),
                options)));

        services.Replace(ServiceDescriptor.Singleton<IEventPublisher>(sp =>
            sp.GetRequiredService<IEventBus>()));

        return services;
    }
}
