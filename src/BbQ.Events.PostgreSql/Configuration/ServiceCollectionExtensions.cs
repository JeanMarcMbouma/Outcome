using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using BbQ.Events.Checkpointing;
using BbQ.Events.PostgreSql.Checkpointing;

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
}
