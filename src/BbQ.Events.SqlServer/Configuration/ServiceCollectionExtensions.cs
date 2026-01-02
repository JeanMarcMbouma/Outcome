using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using BbQ.Events.Checkpointing;
using BbQ.Events.Events;
using BbQ.Events.SqlServer.Checkpointing;
using BbQ.Events.SqlServer.Events;

namespace BbQ.Events.SqlServer.Configuration;

/// <summary>
/// Extension methods for registering SQL Server checkpoint store with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the SQL Server checkpoint store for projection checkpointing.
    /// </summary>
    /// <param name="services">The service collection to register with</param>
    /// <param name="connectionString">The SQL Server connection string</param>
    /// <returns>The service collection for chaining</returns>
    /// <remarks>
    /// This method registers IProjectionCheckpointStore as a singleton using SqlServerProjectionCheckpointStore.
    /// 
    /// Prerequisites:
    /// - SQL Server database must be accessible
    /// - BbQ_ProjectionCheckpoints table must be created (see README for schema)
    /// 
    /// Example usage:
    /// <code>
    /// services.AddInMemoryEventBus();
    /// services.AddProjection&lt;MyProjection&gt;();
    /// services.UseSqlServerCheckpoints("Server=localhost;Database=MyDb;Integrated Security=true;TrustServerCertificate=true");
    /// services.AddProjectionEngine();
    /// </code>
    /// </remarks>
    public static IServiceCollection UseSqlServerCheckpoints(
        this IServiceCollection services, 
        string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentNullException(nameof(connectionString));
        }

        // Replace any existing IProjectionCheckpointStore registration
        services.Replace(ServiceDescriptor.Singleton<IProjectionCheckpointStore>(
            _ => new SqlServerProjectionCheckpointStore(connectionString)));

        return services;
    }

    /// <summary>
    /// Registers the SQL Server event store for event sourcing.
    /// </summary>
    /// <param name="services">The service collection to register with</param>
    /// <param name="connectionString">The SQL Server connection string</param>
    /// <returns>The service collection for chaining</returns>
    /// <remarks>
    /// This method registers IEventStore as a singleton using SqlServerEventStore.
    /// 
    /// Prerequisites:
    /// - SQL Server database must be accessible
    /// - BbQ_Events table must be created (see Schema/CreateEventsTable.sql)
    /// - BbQ_Streams table must be created (see Schema/CreateStreamsTable.sql)
    /// 
    /// Example usage:
    /// <code>
    /// services.UseSqlServerEventStore("Server=localhost;Database=MyDb;Integrated Security=true;TrustServerCertificate=true");
    /// </code>
    /// </remarks>
    public static IServiceCollection UseSqlServerEventStore(
        this IServiceCollection services, 
        string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentNullException(nameof(connectionString));
        }

        return services.UseSqlServerEventStore(options =>
        {
            options.ConnectionString = connectionString;
        });
    }

    /// <summary>
    /// Registers the SQL Server event store for event sourcing with custom options.
    /// </summary>
    /// <param name="services">The service collection to register with</param>
    /// <param name="configureOptions">Action to configure event store options</param>
    /// <returns>The service collection for chaining</returns>
    /// <remarks>
    /// This method registers IEventStore as a singleton using SqlServerEventStore.
    /// 
    /// Prerequisites:
    /// - SQL Server database must be accessible
    /// - BbQ_Events table must be created (see Schema/CreateEventsTable.sql)
    /// - BbQ_Streams table must be created (see Schema/CreateStreamsTable.sql)
    /// 
    /// Example usage:
    /// <code>
    /// services.UseSqlServerEventStore(options =>
    /// {
    ///     options.ConnectionString = "Server=localhost;Database=MyDb;Integrated Security=true";
    ///     options.IncludeMetadata = true;
    ///     options.ReadBatchSize = 500;
    /// });
    /// </code>
    /// </remarks>
    public static IServiceCollection UseSqlServerEventStore(
        this IServiceCollection services,
        Action<SqlServerEventStoreOptions> configureOptions)
    {
        if (configureOptions == null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }

        var options = new SqlServerEventStoreOptions();
        configureOptions(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new ArgumentException("Connection string must be provided in options", nameof(configureOptions));
        }

        // Replace any existing IEventStore registration
        services.Replace(ServiceDescriptor.Singleton<IEventStore>(
            _ => new SqlServerEventStore(options)));

        return services;
    }
}
