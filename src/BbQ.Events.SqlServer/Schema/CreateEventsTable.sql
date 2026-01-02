-- BbQ Events Table
-- Stores individual events with their metadata
-- Each event belongs to a stream and has a sequential position

CREATE TABLE BbQ_Events (
    -- Event identity
    EventId BIGINT IDENTITY(1,1) PRIMARY KEY,
    StreamName NVARCHAR(200) NOT NULL,
    Position BIGINT NOT NULL,
    
    -- Event metadata
    EventType NVARCHAR(500) NOT NULL,
    EventData NVARCHAR(MAX) NOT NULL,
    Metadata NVARCHAR(MAX) NULL,
    
    -- Timestamps
    CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    
    -- Ensure position uniqueness within a stream
    CONSTRAINT UQ_BbQ_Events_Stream_Position UNIQUE (StreamName, Position)
);

-- Index for reading events from a stream by position
CREATE INDEX IX_BbQ_Events_StreamName_Position 
ON BbQ_Events(StreamName, Position);

-- Index for querying by event type (useful for cross-stream queries)
CREATE INDEX IX_BbQ_Events_EventType 
ON BbQ_Events(EventType);

-- Index for time-based queries
CREATE INDEX IX_BbQ_Events_CreatedUtc 
ON BbQ_Events(CreatedUtc);
