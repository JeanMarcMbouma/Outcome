using BbQ.Cqrs;
using Microsoft.Extensions.DependencyInjection;

namespace BbQ.Cqrs.DependencyInjection;

/// <summary>
/// Extension methods for registering CQRS components in the dependency injection container.
/// </summary>
/// <remarks>
/// This static class uses C# 14 extension types to add CQRS registration methods
/// to both IServiceCollection and IMediator.
/// </remarks>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {

        /// <summary>
        /// Registers the BbQ CQRS mediator, dispatchers, and all handlers in the specified assemblies.
        /// </summary>
        /// <param name="services">The service collection to register with</param>
        /// <param name="handlersLifeTime">The lifetime to use for handler instances</param>
        /// <param name="assemblies">Assemblies to scan for handlers</param>
        /// <returns>The service collection for chaining</returns>
        /// <remarks>
        /// This method:
        /// 1. Registers IMediator as a singleton (single instance for the application)
        /// 2. Registers ICommandDispatcher as a singleton
        /// 3. Registers IQueryDispatcher as a singleton
        /// 4. Scans the provided assemblies for all classes implementing IRequestHandler&lt;&gt;
        /// 5. Scans the provided assemblies for all classes implementing IRequestHandler&lt;,&gt;
        /// 6. Registers each handler with its implemented interfaces
        /// 7. Uses the specified lifetime for handlers (scoped by default - one per request in web apps)
        /// 
        /// Pipeline behaviors must be registered separately. Example usage:
        /// <code>
        /// services.AddBbQMediator(
        ///     ServiceLifetime.Scoped,
        ///     typeof(CreateUserCommandHandler).Assembly,
        ///     typeof(GetUserByIdQueryHandler).Assembly
        /// );
        /// 
        /// // Add custom behaviors
        /// services.AddScoped(typeof(IPipelineBehavior&lt;,&gt;), typeof(LoggingBehavior&lt;,&gt;));
        /// services.AddScoped(typeof(IPipelineBehavior&lt;,&gt;), typeof(ValidationBehavior&lt;,&gt;));
        /// services.AddScoped(typeof(IPipelineBehavior&lt;,&gt;), typeof(CachingBehavior&lt;,&gt;));
        /// </code>
        /// 
        /// Behavior registration order affects execution:
        /// - First registered = outermost (executes first, before behaviors registered later)
        /// - Last registered = innermost (executes last, just before the handler)
        /// </remarks>
        public IServiceCollection AddBbQMediator(ServiceLifetime handlersLifeTime,
            params System.Reflection.Assembly[] assemblies)
        {
            // Register the mediator and dispatchers as singleton
            services.AddSingleton<IMediator, Mediator>();
            services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
            services.AddSingleton<IQueryDispatcher, QueryDispatcher>();

            // Scan and register all request handlers from the provided assemblies
            services.Scan(s => s.FromAssemblies(assemblies)
                .AddClasses(c => c.AssignableTo(typeof(IRequestHandler<>)), false)
                .AsImplementedInterfaces()
                .WithLifetime(handlersLifeTime));

            services.Scan(s => s.FromAssemblies(assemblies)
                .AddClasses(c => c.AssignableTo(typeof(IRequestHandler<,>)), false)
                .AsImplementedInterfaces()
                .WithLifetime(handlersLifeTime));

            return services;
        }

        /// <summary>
        /// Registers the BbQ CQRS mediator, dispatchers, and all handlers in the specified assemblies.
        /// </summary>
        /// <param name="services">The service collection to register with</param>
        /// <param name="assemblies">Assemblies to scan for handlers</param>
        /// <returns>The service collection for chaining</returns>
        /// <remarks>
        /// This method:
        /// 1. Registers IMediator as a singleton (single instance for the application)
        /// 2. Registers ICommandDispatcher as a singleton
        /// 3. Registers IQueryDispatcher as a singleton
        /// 4. Scans the provided assemblies for all classes implementing IRequestHandler&lt;&gt;
        /// 5. Scans the provided assemblies for all classes implementing IRequestHandler&lt;,&gt;
        /// 6. Registers each handler with its implemented interfaces
        /// 7. Uses the specified lifetime for handlers (scoped by default - one per request in web apps)
        /// 
        /// Pipeline behaviors must be registered separately. Example usage:
        /// <code>
        /// services.AddBbQMediator(
        ///     typeof(CreateUserCommandHandler).Assembly,
        ///     typeof(GetUserByIdQueryHandler).Assembly
        /// );
        /// 
        /// // Add custom behaviors
        /// services.AddScoped(typeof(IPipelineBehavior&lt;,&gt;), typeof(LoggingBehavior&lt;,&gt;));
        /// services.AddScoped(typeof(IPipelineBehavior&lt;,&gt;), typeof(ValidationBehavior&lt;,&gt;));
        /// services.AddScoped(typeof(IPipelineBehavior&lt;,&gt;), typeof(CachingBehavior&lt;,&gt;));
        /// </code>
        /// 
        /// Behavior registration order affects execution:
        /// - First registered = outermost (executes first, before behaviors registered later)
        /// - Last registered = innermost (executes last, just before the handler)
        /// </remarks>
        public IServiceCollection AddBbQMediator(params System.Reflection.Assembly[] assemblies)
        {
            // Default to scoped lifetime for handlers (typical for web applications)
            // where each HTTP request should have its own handler instance
            return services.AddBbQMediator(ServiceLifetime.Scoped, assemblies);
        }

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
