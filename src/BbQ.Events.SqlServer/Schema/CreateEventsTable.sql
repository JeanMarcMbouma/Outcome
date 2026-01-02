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
    -- This constraint also creates an index for efficient stream reads
    CONSTRAINT UQ_BbQ_Events_Stream_Position UNIQUE (StreamName, Position)
);
