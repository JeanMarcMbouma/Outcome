using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace BbQ.Events.SqlServer.Internal;

/// <summary>
/// Helper methods for SQL Server operations.
/// </summary>
internal static class SqlHelpers
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
    /// Adds a parameter to a SQL command with proper handling of nullable values.
    /// </summary>
    public static void AddParameter(this SqlCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    /// <summary>
    /// Gets a string value from a SqlDataReader, handling null values.
    /// </summary>
    public static string? GetNullableString(this SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    /// <summary>
    /// Gets a long value from a SqlDataReader.
    /// </summary>
    public static long GetLong(this SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.GetInt64(ordinal);
    }

    /// <summary>
    /// Gets a DateTime value from a SqlDataReader.
    /// </summary>
    public static DateTime GetDateTime(this SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.GetDateTime(ordinal);
    }
}
