namespace BbQ.Events.SqlServer.Internal;

/// <summary>
/// SQL Server constants for BbQ.Events.
/// </summary>
internal static class SqlConstants
{
    // Table names
    public const string EventsTable = "BbQ_Events";
    public const string StreamsTable = "BbQ_Streams";
    public const string CheckpointsTable = "BbQ_ProjectionCheckpoints";

    // Column names - Events
    public const string EventId = "EventId";
    public const string StreamName = "StreamName";
    public const string Position = "Position";
    public const string EventType = "EventType";
    public const string EventData = "EventData";
    public const string Metadata = "Metadata";
    public const string CreatedUtc = "CreatedUtc";

    // Column names - Streams
    public const string CurrentPosition = "CurrentPosition";
    public const string Version = "Version";
    public const string LastUpdatedUtc = "LastUpdatedUtc";

    // Column names - Checkpoints
    public const string ProjectionName = "ProjectionName";
    public const string PartitionKey = "PartitionKey";

    // SQL queries - Events
    public const string AppendEventSql = @"
        -- Insert or update stream metadata
        MERGE BbQ_Streams AS target
        USING (SELECT @StreamName AS StreamName) AS source
        ON target.StreamName = source.StreamName
        WHEN MATCHED THEN
            UPDATE SET 
                CurrentPosition = CurrentPosition + 1,
                Version = Version + 1,
                LastUpdatedUtc = SYSUTCDATETIME()
        WHEN NOT MATCHED THEN
            INSERT (StreamName, CurrentPosition, Version, CreatedUtc, LastUpdatedUtc)
            VALUES (@StreamName, 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME());

        -- Get the new position
        DECLARE @NewPosition BIGINT;
        SELECT @NewPosition = CurrentPosition FROM BbQ_Streams WHERE StreamName = @StreamName;

        -- Insert the event
        INSERT INTO BbQ_Events (StreamName, Position, EventType, EventData, Metadata, CreatedUtc)
        VALUES (@StreamName, @NewPosition, @EventType, @EventData, @Metadata, SYSUTCDATETIME());

        -- Return the position
        SELECT @NewPosition AS Position;";

    public const string ReadEventsSql = @"
        SELECT EventId, StreamName, Position, EventType, EventData, Metadata, CreatedUtc
        FROM BbQ_Events
        WHERE StreamName = @StreamName AND Position >= @FromPosition
        ORDER BY Position";

    public const string GetStreamPositionSql = @"
        SELECT CurrentPosition 
        FROM BbQ_Streams 
        WHERE StreamName = @StreamName";
}
