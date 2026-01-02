using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using BbQ.Events.Checkpointing;
using BbQ.Events.Events;
using BbQ.Events.PostgreSql.Checkpointing;
using BbQ.Events.PostgreSql.Events;

namespace BbQ.Events.PostgreSql.Configuration;

/// <summary>
/// Extension methods for registering PostgreSQL checkpoint store with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the PostgreSQL checkpoint store for projection checkpointing.
    /// </summary>
    /// <param name="services">The service collection to register with</param>
    /// <param name="connectionString">The PostgreSQL connection string</param>
    /// <returns>The service collection for chaining</returns>
    /// <remarks>
    /// This method registers IProjectionCheckpointStore as a singleton using PostgreSqlProjectionCheckpointStore.
    /// 
    /// Prerequisites:
    /// - PostgreSQL database must be accessible
    /// - bbq_projection_checkpoints table must be created (see README for schema)
    /// 
    /// Example usage:
    /// <code>
    /// services.AddInMemoryEventBus();
    /// services.AddProjection&lt;MyProjection&gt;();
    /// services.UsePostgreSqlCheckpoints("Host=localhost;Database=mydb;Username=myuser;Password=mypass");
    /// services.AddProjectionEngine();
    /// </code>
    /// </remarks>
    public static IServiceCollection UsePostgreSqlCheckpoints(
        this IServiceCollection services, 
        string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentNullException(nameof(connectionString));
        }

        // Replace any existing IProjectionCheckpointStore registration
        services.Replace(ServiceDescriptor.Singleton<IProjectionCheckpointStore>(
            _ => new PostgreSqlProjectionCheckpointStore(connectionString)));

        return services;
    }

    /// <summary>
    /// Registers the PostgreSQL event store for event sourcing.
    /// </summary>
    /// <param name="services">The service collection to register with</param>
    /// <param name="connectionString">The PostgreSQL connection string</param>
    /// <returns>The service collection for chaining</returns>
    /// <remarks>
    /// This method registers IEventStore as a singleton using PostgreSqlEventStore.
    /// 
    /// Prerequisites:
    /// - PostgreSQL database must be accessible
    /// - bbq_events table must be created (see Schema/CreateEventsTable.sql)
    /// - bbq_streams table must be created (see Schema/CreateStreamsTable.sql)
    /// 
    /// Example usage:
    /// <code>
    /// services.UsePostgreSqlEventStore("Host=localhost;Database=mydb;Username=myuser;Password=mypass");
    /// </code>
    /// </remarks>
    public static IServiceCollection UsePostgreSqlEventStore(
        this IServiceCollection services, 
        string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentNullException(nameof(connectionString));
        }

        return services.UsePostgreSqlEventStore(options =>
        {
            options.ConnectionString = connectionString;
        });
    }

    /// <summary>
    /// Registers the PostgreSQL event store for event sourcing with custom options.
    /// </summary>
    /// <param name="services">The service collection to register with</param>
    /// <param name="configureOptions">Action to configure event store options</param>
    /// <returns>The service collection for chaining</returns>
    /// <remarks>
    /// This method registers IEventStore as a singleton using PostgreSqlEventStore.
    /// 
    /// Prerequisites:
    /// - PostgreSQL database must be accessible
    /// - bbq_events table must be created (see Schema/CreateEventsTable.sql)
    /// - bbq_streams table must be created (see Schema/CreateStreamsTable.sql)
    /// 
    /// Example usage:
    /// <code>
    /// services.UsePostgreSqlEventStore(options =>
    /// {
    ///     options.ConnectionString = "Host=localhost;Database=mydb;Username=myuser;Password=mypass";
    ///     options.IncludeMetadata = true;
    /// });
    /// </code>
    /// </remarks>
    public static IServiceCollection UsePostgreSqlEventStore(
        this IServiceCollection services,
        Action<PostgreSqlEventStoreOptions> configureOptions)
    {
        if (configureOptions == null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }

        var options = new PostgreSqlEventStoreOptions();
        configureOptions(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new ArgumentException("Connection string must be configured via the configureOptions action.", nameof(configureOptions));
        }

        // Replace any existing IEventStore registration
        services.Replace(ServiceDescriptor.Singleton<IEventStore>(
            _ => new PostgreSqlEventStore(options)));

        return services;
    }
}
