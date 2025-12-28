using BbQ.Events;
using Microsoft.Extensions.DependencyInjection;

namespace BbQ.Events.DependencyInjection;

/// <summary>
/// Extension methods for registering event bus components in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the in-memory event bus for pub/sub functionality.
        /// </summary>
        /// <param name="services">The service collection to register with</param>
        /// <returns>The service collection for chaining</returns>
        /// <remarks>
        /// This method registers:
        /// 1. IEventBus as a singleton using InMemoryEventBus implementation
        /// 2. IEventPublisher as a singleton (resolves to the same IEventBus instance)
        /// 
        /// The in-memory event bus:
        /// - Is suitable for single-process applications
        /// - Does not persist events (lost on restart)
        /// - Uses System.Threading.Channels for thread-safe pub/sub
        /// - Supports multiple handlers and subscribers per event type
        /// 
        /// Event handlers and subscribers must be registered separately:
        /// <code>
        /// services.AddInMemoryEventBus();
        /// 
        /// // Register event handlers (auto-discovered by source generator)
        /// services.AddYourAssemblyNameHandlers();
        /// 
        /// // Or manually register handlers
        /// services.AddScoped&lt;IEventHandler&lt;UserCreated&gt;, SendWelcomeEmailHandler&gt;();
        /// </code>
        /// 
        /// For distributed systems, implement a custom IEventBus using
        /// a message broker like RabbitMQ, Azure Service Bus, or Kafka.
        /// </remarks>
        public IServiceCollection AddInMemoryEventBus()
        {
            // Register the event bus as singleton (single instance for the application)
            services.AddSingleton<IEventBus, InMemoryEventBus>();
            
            // Register IEventPublisher to resolve to the same IEventBus instance
            services.AddSingleton<IEventPublisher>(sp => sp.GetRequiredService<IEventBus>());
            
            return services;
        }
    }
}
