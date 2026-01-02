using BbQ.Events.Checkpointing;
using Microsoft.Data.SqlClient;

namespace BbQ.Events.SqlServer.Checkpointing;

/// <summary>
/// SQL Server implementation of projection checkpoint storage.
/// 
/// This implementation provides:
/// - Durable persistence of projection checkpoints in SQL Server
/// - Atomic upserts via MERGE statement
/// - Thread-safe parallel processing support
/// - Support for both partitioned and non-partitioned projections
/// </summary>
/// <remarks>
/// This checkpoint store uses raw ADO.NET for minimal dependencies and maximum performance.
/// Checkpoints are stored in a table with atomic MERGE operations to prevent race conditions.
/// 
/// The implementation supports partitioned projections through the database schema, though
/// the current IProjectionCheckpointStore interface doesn't expose partition key parameters.
/// The PartitionKey column is nullable and defaults to NULL for non-partitioned projections.
/// 
/// Connection handling:
/// - Each operation opens a new connection (connection pooling is handled by ADO.NET)
/// - Operations are fully async for optimal scalability
/// - Connections are properly disposed in all code paths
/// </remarks>
public class SqlServerProjectionCheckpointStore : IProjectionCheckpointStore
{
    private readonly string _connectionString;

    /// <summary>
    /// Creates a new SQL Server checkpoint store.
    /// </summary>
    /// <param name="connectionString">The SQL Server connection string</param>
    /// <exception cref="ArgumentNullException">Thrown when connectionString is null or empty</exception>
    public SqlServerProjectionCheckpointStore(string connectionString)
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

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Position 
            FROM BbQ_ProjectionCheckpoints 
            WHERE ProjectionName = @ProjectionName 
              AND PartitionKey IS NULL";
        
        command.Parameters.AddWithValue("@ProjectionName", projectionName);

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
    /// This method uses a MERGE statement for atomic upsert operations.
    /// It's safe to call concurrently from multiple threads/processes.
    /// </remarks>
    public async ValueTask SaveCheckpointAsync(string projectionName, long checkpoint, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(projectionName))
        {
            throw new ArgumentNullException(nameof(projectionName));
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            MERGE BbQ_ProjectionCheckpoints AS target
            USING (SELECT @ProjectionName AS ProjectionName, @PartitionKey AS PartitionKey) AS source
            ON target.ProjectionName = source.ProjectionName 
               AND target.PartitionKey IS NULL 
               AND source.PartitionKey IS NULL
            WHEN MATCHED THEN
                UPDATE SET Position = @Position, LastUpdatedUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (ProjectionName, PartitionKey, Position, LastUpdatedUtc)
                VALUES (@ProjectionName, @PartitionKey, @Position, SYSUTCDATETIME());";

        command.Parameters.AddWithValue("@ProjectionName", projectionName);
        command.Parameters.AddWithValue("@PartitionKey", DBNull.Value);
        command.Parameters.AddWithValue("@Position", checkpoint);

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

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            DELETE FROM BbQ_ProjectionCheckpoints 
            WHERE ProjectionName = @ProjectionName 
              AND PartitionKey IS NULL";
        
        command.Parameters.AddWithValue("@ProjectionName", projectionName);

        await command.ExecuteNonQueryAsync(ct);
    }
}
