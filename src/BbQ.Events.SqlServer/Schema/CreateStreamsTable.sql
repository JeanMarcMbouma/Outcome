-- BbQ Streams Table
-- Stores stream metadata and current position
-- Used for optimistic concurrency control and position tracking

CREATE TABLE BbQ_Streams (
    StreamName NVARCHAR(200) PRIMARY KEY,
    CurrentPosition BIGINT NOT NULL DEFAULT -1,
    Version INT NOT NULL DEFAULT 0,
    CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    LastUpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

-- Index for version-based queries (optimistic concurrency)
CREATE INDEX IX_BbQ_Streams_Version 
ON BbQ_Streams(Version);

-- Index for time-based queries
CREATE INDEX IX_BbQ_Streams_LastUpdatedUtc 
ON BbQ_Streams(LastUpdatedUtc);
