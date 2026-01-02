using BbQ.Events.Checkpointing;
using Npgsql;

namespace BbQ.Events.PostgreSql.Checkpointing;

/// <summary>
/// PostgreSQL implementation of projection checkpoint storage.
/// 
/// This implementation provides:
/// - Durable persistence of projection checkpoints in PostgreSQL
/// - Atomic upserts via INSERT ... ON CONFLICT statement
/// - Thread-safe parallel processing support
/// - Support for both partitioned and non-partitioned projections
/// </summary>
/// <remarks>
/// This checkpoint store uses Npgsql (PostgreSQL ADO.NET provider) for minimal dependencies and maximum performance.
/// Checkpoints are stored in a table with atomic INSERT ... ON CONFLICT operations to prevent race conditions.
/// 
/// The implementation supports partitioned projections through the database schema, though
/// the current IProjectionCheckpointStore interface doesn't expose partition key parameters.
/// The partition_key column is nullable and defaults to NULL for non-partitioned projections.
/// 
/// Connection handling:
/// - Each operation opens a new connection (connection pooling is handled by Npgsql)
/// - Operations are fully async for optimal scalability
/// - Connections are properly disposed in all code paths
/// </remarks>
public class PostgreSqlProjectionCheckpointStore : IProjectionCheckpointStore
{
    private readonly string _connectionString;

    /// <summary>
    /// Creates a new PostgreSQL checkpoint store.
    /// </summary>
    /// <param name="connectionString">The PostgreSQL connection string</param>
    /// <exception cref="ArgumentNullException">Thrown when connectionString is null or empty</exception>
    public PostgreSqlProjectionCheckpointStore(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentNullException(nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    /// <summary>
    /// Gets the last checkpoint for a specific projection.
    /// </summary>
    /// <param name="projectionName">The unique name of the projection</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The checkpoint position, or null if no checkpoint exists</returns>
    public async ValueTask<long?> GetCheckpointAsync(string projectionName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(projectionName))
        {
            throw new ArgumentNullException(nameof(projectionName));
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT position 
            FROM bbq_projection_checkpoints 
            WHERE projection_name = @projection_name 
              AND partition_key IS NULL";
        
        command.Parameters.AddWithValue("@projection_name", projectionName);

        var result = await command.ExecuteScalarAsync(ct);
        
        return result == null || result == DBNull.Value 
            ? null 
            : Convert.ToInt64(result);
    }

    /// <summary>
    /// Saves a checkpoint for a specific projection.
    /// </summary>
    /// <param name="projectionName">The unique name of the projection</param>
    /// <param name="checkpoint">The checkpoint position to save</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A task that completes when the checkpoint has been saved</returns>
    /// <remarks>
    /// This method uses PostgreSQL's INSERT ... ON CONFLICT statement for atomic upsert operations.
    /// It's safe to call concurrently from multiple threads/processes.
    /// </remarks>
    public async ValueTask SaveCheckpointAsync(string projectionName, long checkpoint, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(projectionName))
        {
            throw new ArgumentNullException(nameof(projectionName));
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO bbq_projection_checkpoints (projection_name, partition_key, position, updated_at)
            VALUES (@projection_name, NULL, @position, NOW())
            ON CONFLICT (projection_name, partition_key)
            DO UPDATE SET position = EXCLUDED.position, updated_at = NOW()";

        command.Parameters.AddWithValue("@projection_name", projectionName);
        command.Parameters.AddWithValue("@position", checkpoint);

        await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Resets the checkpoint for a specific projection.
    /// </summary>
    /// <param name="projectionName">The unique name of the projection</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A task that completes when the checkpoint has been reset</returns>
    public async ValueTask ResetCheckpointAsync(string projectionName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(projectionName))
        {
            throw new ArgumentNullException(nameof(projectionName));
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            DELETE FROM bbq_projection_checkpoints 
            WHERE projection_name = @projection_name 
              AND partition_key IS NULL";
        
        command.Parameters.AddWithValue("@projection_name", projectionName);

        await command.ExecuteNonQueryAsync(ct);
    }
}
