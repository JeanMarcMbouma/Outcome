namespace BbQ.Events.PostgreSql.Internal;

/// <summary>
/// PostgreSQL constants for BbQ.Events.
/// </summary>
internal static class PostgreSqlConstants
{
    // Table names
    public const string EventsTable = "bbq_events";
    public const string StreamsTable = "bbq_streams";
    public const string CheckpointsTable = "bbq_projection_checkpoints";

    // Column names - Events
    public const string EventId = "event_id";
    public const string StreamName = "stream_name";
    public const string Position = "position";
    public const string EventType = "event_type";
    public const string EventData = "event_data";
    public const string Metadata = "metadata";
    public const string CreatedUtc = "created_utc";

    // Column names - Streams
    public const string CurrentPosition = "current_position";
    public const string Version = "version";
    public const string LastUpdatedUtc = "last_updated_utc";

    // Column names - Checkpoints
    public const string ProjectionName = "projection_name";
    public const string PartitionKey = "partition_key";

    // SQL queries - Events
    public const string AppendEventSql = @"
        -- Insert or update stream metadata and capture the new position atomically
        INSERT INTO bbq_streams (stream_name, current_position, version, created_utc, last_updated_utc)
        VALUES (@stream_name, 0, 1, NOW(), NOW())
        ON CONFLICT (stream_name)
        DO UPDATE SET 
            current_position = bbq_streams.current_position + 1,
            version = bbq_streams.version + 1,
            last_updated_utc = NOW()
        RETURNING current_position INTO @new_position;

        -- Insert the event
        INSERT INTO bbq_events (stream_name, position, event_type, event_data, metadata, created_utc)
        VALUES (@stream_name, @new_position, @event_type, @event_data, @metadata, NOW());

        -- Return the position
        SELECT @new_position AS position;";

    // Simplified approach using CTE
    public const string AppendEventSqlSimplified = @"
        WITH updated_stream AS (
            INSERT INTO bbq_streams (stream_name, current_position, version, created_utc, last_updated_utc)
            VALUES (@stream_name, 0, 1, NOW(), NOW())
            ON CONFLICT (stream_name)
            DO UPDATE SET 
                current_position = bbq_streams.current_position + 1,
                version = bbq_streams.version + 1,
                last_updated_utc = NOW()
            RETURNING current_position
        ),
        inserted_event AS (
            INSERT INTO bbq_events (stream_name, position, event_type, event_data, metadata, created_utc)
            SELECT @stream_name, current_position, @event_type, @event_data, @metadata, NOW()
            FROM updated_stream
            RETURNING position
        )
        SELECT position FROM inserted_event;";

    public const string ReadEventsSql = @"
        SELECT event_id, stream_name, position, event_type, event_data, metadata, created_utc
        FROM bbq_events
        WHERE stream_name = @stream_name AND position >= @from_position
        ORDER BY position";

    public const string GetStreamPositionSql = @"
        SELECT current_position 
        FROM bbq_streams 
        WHERE stream_name = @stream_name";
}
