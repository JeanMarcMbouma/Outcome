using System.Text.Json;

namespace BbQ.Events.PostgreSql.Events;

/// <summary>
/// Configuration options for PostgreSQL event store.
/// </summary>
public class PostgreSqlEventStoreOptions
{
    /// <summary>
    /// Gets or sets the connection string for PostgreSQL.
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
}
