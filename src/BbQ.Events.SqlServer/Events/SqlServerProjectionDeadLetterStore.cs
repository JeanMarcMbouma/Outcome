using System.Runtime.CompilerServices;
using System.Text.Json;
using BbQ.Events.Engine;
using Microsoft.Data.SqlClient;

namespace BbQ.Events.SqlServer.Events;

/// <summary>Durable, append-only SQL Server quarantine. Call InitializeAsync during deployment.</summary>
/// <remarks>Uses its own transaction; checkpoint storage is not atomically enlisted. Retain records for recovery and replay.</remarks>
public sealed class SqlServerProjectionDeadLetterStore(string connectionString) : IProjectionDeadLetterStore
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "IF OBJECT_ID(N'dbo.BbQ_ProjectionDeadLetters', N'U') IS NULL CREATE TABLE dbo.BbQ_ProjectionDeadLetters (Id char(64) NOT NULL PRIMARY KEY, Body nvarchar(max) NOT NULL);";
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task PutAsync(ProjectionDeadLetter entry, CancellationToken ct = default)
    {
        if (entry.Id != ProjectionFailureProcessor.GetDeadLetterId(entry.ProjectionName, entry.PartitionKey, entry.EventId))
            throw new ArgumentException("Dead-letter ID does not match its identity.", nameof(entry));
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO dbo.BbQ_ProjectionDeadLetters (Id, Body) VALUES (@id, @body)";
        command.Parameters.AddWithValue("@id", entry.Id);
        command.Parameters.AddWithValue("@body", JsonSerializer.Serialize(entry));
        try
        {
            // A single statement commits atomically. The unique key arbitrates competing deliveries.
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            var existing = await GetAsync(entry.Id, ct).ConfigureAwait(false);
            if (existing == null || existing.ProjectionName != entry.ProjectionName || existing.PartitionKey != entry.PartitionKey ||
                existing.EventId != entry.EventId || existing.Event != entry.Event)
                throw new InvalidOperationException("Dead-letter identity collision with different event data.", exception);
        }
    }

    public async Task<ProjectionDeadLetter?> GetAsync(string id, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Body FROM dbo.BbQ_ProjectionDeadLetters WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);
        var body = await command.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
        return body == null ? null : JsonSerializer.Deserialize<ProjectionDeadLetter>(body);
    }

    public async IAsyncEnumerable<ProjectionDeadLetter> ReadAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Body FROM dbo.BbQ_ProjectionDeadLetters ORDER BY Id";
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            yield return JsonSerializer.Deserialize<ProjectionDeadLetter>(reader.GetString(0))!;
    }
}
