using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using BbQ.Events;

namespace BbQ.Events.SqlServer;

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
}
