using BbQ.Events.Checkpointing;
using BbQ.Events.Engine;
using BbQ.Events.Events;
using BbQ.Events.Projections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BbQ.Events.Configuration;

/// <summary>
/// Extension methods for registering event bus and projection components in the dependency injection container.
/// </summary>
/// <remarks>
/// This static class uses extension types to add event bus and projection registration methods
/// to IServiceCollection.
/// </remarks>
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

        /// <summary>
        /// Registers a projection handler with the dependency injection container.
        /// </summary>
        /// <typeparam name="TProjection">The projection handler type</typeparam>
        /// <param name="services">The service collection to register with</param>
        /// <param name="lifetime">The lifetime to use for the projection handler (default: Scoped)</param>
        /// <returns>The service collection for chaining</returns>
        /// <remarks>
        /// This method registers the projection handler for all event types it handles.
        /// The projection must implement at least one IProjectionHandler&lt;TEvent&gt; or
        /// IPartitionedProjectionHandler&lt;TEvent&gt; interface.
        /// 
        /// Projection options are read from the [Projection] attribute if present. 
        /// For manual registration without the attribute, use the overload that accepts an options configuration.
        /// 
        /// Example usage:
        /// <code>
        /// services.AddInMemoryEventBus();
        /// services.AddProjection&lt;UserProfileProjection&gt;();
        /// services.AddProjectionEngine();
        /// </code>
        /// </remarks>
        public IServiceCollection AddProjection<TProjection>(ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TProjection : class
        {
            return services.AddProjection<TProjection>(null, lifetime);
        }
        
        /// <summary>
        /// Registers a projection handler with the dependency injection container with custom options.
        /// </summary>
        /// <typeparam name="TProjection">The projection handler type</typeparam>
        /// <param name="services">The service collection to register with</param>
        /// <param name="configureOptions">Action to configure projection options</param>
        /// <param name="lifetime">The lifetime to use for the projection handler (default: Scoped)</param>
        /// <returns>The service collection for chaining</returns>
        /// <remarks>
        /// This method registers the projection handler and allows you to configure options programmatically.
        /// Options configured here take precedence over the [Projection] attribute.
        /// 
        /// This overload is intended for manual registration scenarios. When using source generators,
        /// prefer configuring options via the [Projection] attribute instead.
        /// 
        /// Example usage:
        /// <code>
        /// services.AddInMemoryEventBus();
        /// services.AddProjection&lt;UserProfileProjection&gt;(options =&gt; 
        /// {
        ///     options.MaxDegreeOfParallelism = 4;
        ///     options.CheckpointBatchSize = 50;
        /// });
        /// services.AddProjectionEngine();
        /// </code>
        /// </remarks>
        public IServiceCollection AddProjection<TProjection>(
            Action<ProjectionOptions>? configureOptions,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TProjection : class
        {
            // Register the projection handler itself
            services.Add(new ServiceDescriptor(typeof(TProjection), typeof(TProjection), lifetime));

            // Register for each IProjectionHandler<TEvent> interface it implements
            var projectionType = typeof(TProjection);
            var projectionInterfaces = projectionType.GetInterfaces()
                .Where(iface => iface.IsGenericType &&
                    (iface.GetGenericTypeDefinition() == typeof(IProjectionHandler<>) ||
                     iface.GetGenericTypeDefinition() == typeof(IPartitionedProjectionHandler<>)));

            // Configure options
            ProjectionOptions? options = null;
            if (configureOptions != null)
            {
                options = new ProjectionOptions { ProjectionName = projectionType.Name };
                configureOptions(options);
            }

            foreach (var iface in projectionInterfaces)
            {
                // Register the interface to resolve to the projection instance
                services.Add(new ServiceDescriptor(iface, sp => sp.GetRequiredService<TProjection>(), lifetime));
                
                // Register in the projection handler registry for the engine to discover
                var eventType = iface.GenericTypeArguments[0];
                ProjectionHandlerRegistry.Register(eventType, iface, projectionType, options);
            }

            return services;
        }

        /// <summary>
        /// Registers the projection engine for running projections.
        /// </summary>
        /// <param name="services">The service collection to register with</param>
        /// <returns>The service collection for chaining</returns>
        /// <remarks>
        /// This method registers:
        /// 1. IProjectionCheckpointStore as a singleton (in-memory implementation)
        /// 2. IProjectionMonitor as a singleton (in-memory implementation)
        /// 3. IProjectionEngine as a singleton
        /// 4. IProjectionRebuilder as a singleton
        /// 5. IReplayService as a singleton
        /// 
        /// The projection engine must be run manually or as a hosted service:
        /// <code>
        /// var engine = serviceProvider.GetRequiredService&lt;IProjectionEngine&gt;();
        /// await engine.RunAsync(cancellationToken);
        /// </code>
        /// 
        /// For production use, replace the in-memory checkpoint store with a durable implementation:
        /// <code>
        /// services.AddSingleton&lt;IProjectionCheckpointStore, SqlProjectionCheckpointStore&gt;();
        /// services.AddProjectionEngine();
        /// </code>
        /// </remarks>
        public IServiceCollection AddProjectionEngine()
        {
            // Register checkpoint store if not already registered
            services.TryAddSingleton<IProjectionCheckpointStore, InMemoryProjectionCheckpointStore>();
            
            // Register projection monitor if not already registered
            services.TryAddSingleton<IProjectionMonitor, InMemoryProjectionMonitor>();
            
            // Register the projection engine
            services.TryAddSingleton<IProjectionEngine, DefaultProjectionEngine>();
            
            // Register the projection rebuilder
            services.TryAddSingleton<IProjectionRebuilder, DefaultProjectionRebuilder>();
            
            // Register the replay service
            services.TryAddSingleton<IReplayService, DefaultReplayService>();
            
            return services;
        }

        /// <summary>
        /// Registers all projections from the specified assembly that are marked with [Projection] attribute.
        /// </summary>
        /// <param name="services">The service collection to register with</param>
        /// <param name="assembly">The assembly to scan for projections</param>
        /// <param name="lifetime">The lifetime to use for projection handlers (default: Scoped)</param>
        /// <returns>The service collection for chaining</returns>
        /// <remarks>
        /// This method scans the assembly for classes marked with [Projection] attribute
        /// and registers them. This is useful for manual registration when source generators
        /// are not available or desired.
        /// 
        /// Example usage:
        /// <code>
        /// services.AddInMemoryEventBus();
        /// services.AddProjectionsFromAssembly(typeof(Program).Assembly);
        /// services.AddProjectionEngine();
        /// </code>
        /// 
        /// Note: The source generator provides a more efficient alternative by generating
        /// registration code at compile time.
        /// </remarks>
        public IServiceCollection AddProjectionsFromAssembly(
            System.Reflection.Assembly assembly,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            var projectionTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => t.GetCustomAttributes(typeof(ProjectionAttribute), false).Any())
                .Where(t => t.GetInterfaces().Any(i => 
                    i.IsGenericType && 
                    (i.GetGenericTypeDefinition() == typeof(IProjectionHandler<>) ||
                     i.GetGenericTypeDefinition() == typeof(IPartitionedProjectionHandler<>) ||
                     i.GetGenericTypeDefinition() == typeof(IProjectionBatchHandler<>))));

            foreach (var projectionType in projectionTypes)
            {
                // Register the projection type itself
                services.Add(new ServiceDescriptor(projectionType, projectionType, lifetime));

                // Register for each handler interface
                var projectionInterfaces = projectionType.GetInterfaces()
                    .Where(iface => iface.IsGenericType &&
                        (iface.GetGenericTypeDefinition() == typeof(IProjectionHandler<>) ||
                         iface.GetGenericTypeDefinition() == typeof(IPartitionedProjectionHandler<>) ||
                         iface.GetGenericTypeDefinition() == typeof(IProjectionBatchHandler<>)));

                foreach (var iface in projectionInterfaces)
                {
                    // Register the interface to resolve to the projection instance
                    services.Add(new ServiceDescriptor(iface, sp => sp.GetRequiredService(projectionType), lifetime));
                    
                    // Register in the projection handler registry for the engine to discover
                    var eventType = iface.GenericTypeArguments[0];
                    ProjectionHandlerRegistry.Register(eventType, iface, projectionType);
                }
            }

            return services;
        }

        /// <summary>
        /// Registers a batch projection handler with the dependency injection container.
        /// </summary>
        /// <typeparam name="TProjection">The batch projection handler type</typeparam>
        /// <param name="services">The service collection to register with</param>
        /// <param name="lifetime">The lifetime to use for the projection handler (default: Scoped)</param>
        /// <returns>The service collection for chaining</returns>
        /// <remarks>
        /// This method registers a batch projection handler that receives events in batches.
        /// The projection must implement at least one IProjectionBatchHandler&lt;TEvent&gt; interface.
        /// 
        /// Example usage:
        /// <code>
        /// services.AddInMemoryEventBus();
        /// services.AddBatchProjection&lt;UserProfileBatchProjection&gt;();
        /// services.AddProjectionService();
        /// </code>
        /// </remarks>
        public IServiceCollection AddBatchProjection<TProjection>(ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TProjection : class
        {
            return services.AddBatchProjection<TProjection>(null, lifetime);
        }

        /// <summary>
        /// Registers a batch projection handler with custom service options.
        /// </summary>
        /// <typeparam name="TProjection">The batch projection handler type</typeparam>
        /// <param name="services">The service collection to register with</param>
        /// <param name="configureOptions">Action to configure projection service options</param>
        /// <param name="lifetime">The lifetime to use for the projection handler (default: Scoped)</param>
        /// <returns>The service collection for chaining</returns>
        /// <remarks>
        /// This method registers a batch projection handler and allows configuration of
        /// batch size, timeout, parallelism, and automatic checkpointing.
        /// 
        /// Example usage:
        /// <code>
        /// services.AddBatchProjection&lt;UserProfileBatchProjection&gt;(options =&gt;
        /// {
        ///     options.BatchSize = 50;
        ///     options.BatchTimeout = TimeSpan.FromSeconds(5);
        ///     options.MaxDegreeOfParallelism = 4;
        ///     options.AutoCheckpoint = true;
        /// });
        /// </code>
        /// </remarks>
        public IServiceCollection AddBatchProjection<TProjection>(
            Action<ProjectionServiceOptions>? configureOptions,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TProjection : class
        {
            // Register the projection handler itself
            services.Add(new ServiceDescriptor(typeof(TProjection), typeof(TProjection), lifetime));

            var projectionType = typeof(TProjection);
            var batchInterfaces = projectionType.GetInterfaces()
                .Where(iface => iface.IsGenericType &&
                    iface.GetGenericTypeDefinition() == typeof(IProjectionBatchHandler<>));

            // Configure options
            ProjectionServiceOptions? options = null;
            if (configureOptions != null)
            {
                options = new ProjectionServiceOptions { ProjectionName = projectionType.Name };
                configureOptions(options);
            }

            foreach (var iface in batchInterfaces)
            {
                // Register the interface to resolve to the projection instance
                services.Add(new ServiceDescriptor(iface, sp => sp.GetRequiredService<TProjection>(), lifetime));

                // Register in the projection handler registry
                var eventType = iface.GenericTypeArguments[0];
                ProjectionHandlerRegistry.RegisterBatch(eventType, iface, projectionType, options);
            }

            return services;
        }

        /// <summary>
        /// Registers the projection service for batch processing with automatic checkpointing.
        /// </summary>
        /// <param name="services">The service collection to register with</param>
        /// <returns>The service collection for chaining</returns>
        /// <remarks>
        /// This method registers:
        /// 1. IProjectionCheckpointStore as a singleton (in-memory, if not already registered)
        /// 2. IProjectionMonitor as a singleton (in-memory, if not already registered)
        /// 3. IProjectionService as a singleton (DefaultProjectionService)
        ///
        /// The projection service must be run manually or as a hosted service:
        /// <code>
        /// services.AddInMemoryEventBus();
        /// services.AddBatchProjection&lt;UserProfileBatchProjection&gt;(options =&gt;
        /// {
        ///     options.BatchSize = 50;
        ///     options.BatchTimeout = TimeSpan.FromSeconds(5);
        /// });
        /// services.AddProjectionService();
        /// 
        /// var service = serviceProvider.GetRequiredService&lt;IProjectionService&gt;();
        /// await service.RunAsync(cancellationToken);
        /// </code>
        /// </remarks>
        public IServiceCollection AddProjectionService()
        {
            // Register checkpoint store if not already registered
            services.TryAddSingleton<IProjectionCheckpointStore, InMemoryProjectionCheckpointStore>();

            // Register projection monitor if not already registered
            services.TryAddSingleton<IProjectionMonitor, InMemoryProjectionMonitor>();

            // Register the projection service
            services.TryAddSingleton<IProjectionService, DefaultProjectionService>();

            return services;
        }
    }
}
