using System.Text.Json;

namespace BbQ.Events.SqlServer.Events;

/// <summary>
/// Configuration options for SQL Server event store.
/// </summary>
public class SqlServerEventStoreOptions
{
    /// <summary>
    /// Gets or sets the connection string for SQL Server.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JSON serializer options for event data.
    /// </summary>
    /// <remarks>
    /// If not provided, defaults to camelCase property naming.
    /// </remarks>
    public JsonSerializerOptions? JsonSerializerOptions { get; set; }

    /// <summary>
    /// Gets or sets whether to include metadata in stored events.
    /// </summary>
    /// <remarks>
    /// Metadata can include correlation IDs, causation IDs, timestamps, etc.
    /// Default is false.
    /// </remarks>
    public bool IncludeMetadata { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to automatically create the database schema if it doesn't exist.
    /// </summary>
    /// <remarks>
    /// When enabled, the event store will check for and create the required tables
    /// (BbQ_Events, BbQ_Streams, BbQ_ProjectionCheckpoints) on first use.
    /// Default is false for safety in production environments.
    /// </remarks>
    public bool AutoCreateSchema { get; set; } = false;
}
