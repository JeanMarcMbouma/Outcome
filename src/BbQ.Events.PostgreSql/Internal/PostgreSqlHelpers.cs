using System.Text.Json;
using Npgsql;

namespace BbQ.Events.PostgreSql.Internal;

/// <summary>
/// Helper methods for PostgreSQL operations.
/// </summary>
internal static class PostgreSqlHelpers
{
    /// <summary>
    /// Default JSON serializer options for event data.
    /// </summary>
    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Serializes an object to JSON string.
    /// </summary>
    public static string SerializeToJson<T>(T obj, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Serialize(obj, options ?? DefaultJsonOptions);
    }

    /// <summary>
    /// Deserializes a JSON string to an object.
    /// </summary>
    public static T DeserializeFromJson<T>(string json, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Deserialize<T>(json, options ?? DefaultJsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize JSON");
    }

    /// <summary>
    /// Adds a parameter to a PostgreSQL command with proper handling of nullable values.
    /// </summary>
    public static void AddParameter(this NpgsqlCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    /// <summary>
    /// Gets a long value from a NpgsqlDataReader.
    /// </summary>
    public static long GetLong(this NpgsqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.GetInt64(ordinal);
    }
}
