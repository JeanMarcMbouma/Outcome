using System.Runtime.CompilerServices;
using System.Text.Json;
using BbQ.Events.Events;
using BbQ.Events.SqlServer.Internal;
using Microsoft.Data.SqlClient;

namespace BbQ.Events.SqlServer.Events;

/// <summary>
/// SQL Server implementation of IEventStore.
/// </summary>
/// <remarks>
/// This implementation provides:
/// - Durable event persistence in SQL Server
/// - Sequential position tracking per stream
/// - Atomic append operations
/// - Efficient event replay via streaming reads
/// - JSON serialization of event data
/// - Support for event metadata
/// 
/// Connection handling:
/// - Each operation opens a new connection (connection pooling is handled by ADO.NET)
/// - Operations are fully async for optimal scalability
/// - Connections are properly disposed in all code paths
/// 
/// Prerequisites:
/// - BbQ_Events table must exist (see Schema/CreateEventsTable.sql)
/// - BbQ_Streams table must exist (see Schema/CreateStreamsTable.sql)
/// </remarks>
public class SqlServerEventStore : IEventStore
{
    private readonly SqlServerEventStoreOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Creates a new SQL Server event store.
    /// </summary>
    /// <param name="options">Configuration options</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null</exception>
    /// <exception cref="ArgumentException">Thrown when connection string is null or empty</exception>
    public SqlServerEventStore(SqlServerEventStoreOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty", nameof(options));
        }

        _jsonOptions = _options.JsonSerializerOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Appends an event to a stream.
    /// </summary>
    /// <typeparam name="TEvent">The type of event</typeparam>
    /// <param name="stream">The stream name</param>
    /// <param name="event">The event to append</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The position of the appended event</returns>
    /// <exception cref="ArgumentException">Thrown when stream name is null or empty</exception>
    /// <exception cref="ArgumentNullException">Thrown when event is null</exception>
    public async Task<long> AppendAsync<TEvent>(string stream, TEvent @event, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(stream))
        {
            throw new ArgumentException("Stream name cannot be null or empty", nameof(stream));
        }

        if (@event == null)
        {
            throw new ArgumentNullException(nameof(@event));
        }

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = SqlConstants.AppendEventSql;

        var eventType = typeof(TEvent).FullName ?? typeof(TEvent).Name;
        var eventData = SqlHelpers.SerializeToJson(@event, _jsonOptions);
        
        command.AddParameter("@StreamName", stream);
        command.AddParameter("@EventType", eventType);
        command.AddParameter("@EventData", eventData);
        command.AddParameter("@Metadata", _options.IncludeMetadata ? CreateMetadata() : null);

        var result = await command.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result);
    }

    /// <summary>
    /// Reads events from a stream starting at a given position.
    /// </summary>
    /// <typeparam name="TEvent">The type of events to read</typeparam>
    /// <param name="stream">The stream name</param>
    /// <param name="fromPosition">The position to start reading from (inclusive)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>An async enumerable of events with their positions</returns>
    /// <exception cref="ArgumentException">Thrown when stream name is null or empty</exception>
    public async IAsyncEnumerable<StoredEvent<TEvent>> ReadAsync<TEvent>(
        string stream, 
        long fromPosition = 0,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(stream))
        {
            throw new ArgumentException("Stream name cannot be null or empty", nameof(stream));
        }

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = SqlConstants.ReadEventsSql;
        command.AddParameter("@StreamName", stream);
        command.AddParameter("@FromPosition", fromPosition);

        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var position = reader.GetLong(SqlConstants.Position);
            var eventType = reader.GetString(reader.GetOrdinal(SqlConstants.EventType));
            var eventData = reader.GetString(reader.GetOrdinal(SqlConstants.EventData));

            // Only deserialize if the event type matches
            // This allows for type filtering when reading from streams with multiple event types
            var expectedType = typeof(TEvent).FullName ?? typeof(TEvent).Name;
            if (eventType == expectedType)
            {
                var @event = SqlHelpers.DeserializeFromJson<TEvent>(eventData, _jsonOptions);
                yield return new StoredEvent<TEvent>(position, @event);
            }
        }
    }

    /// <summary>
    /// Gets the current position (last event position) in a stream.
    /// </summary>
    /// <param name="stream">The stream name</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The current position, or null if the stream doesn't exist</returns>
    /// <exception cref="ArgumentException">Thrown when stream name is null or empty</exception>
    public async Task<long?> GetStreamPositionAsync(string stream, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(stream))
        {
            throw new ArgumentException("Stream name cannot be null or empty", nameof(stream));
        }

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = SqlConstants.GetStreamPositionSql;
        command.AddParameter("@StreamName", stream);

        var result = await command.ExecuteScalarAsync(ct);
        
        return result == null || result == DBNull.Value 
            ? null 
            : Convert.ToInt64(result);
    }

    /// <summary>
    /// Creates metadata for an event.
    /// </summary>
    private string CreateMetadata()
    {
        var metadata = new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.UtcNow,
            ["server"] = Environment.MachineName
        };

        return SqlHelpers.SerializeToJson(metadata, _jsonOptions);
    }
}
