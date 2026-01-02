-- BbQ Streams Table
-- Stores stream metadata and current position
-- Used for position tracking

CREATE TABLE BbQ_Streams (
    StreamName NVARCHAR(200) PRIMARY KEY,
    CurrentPosition BIGINT NOT NULL DEFAULT -1,
    Version INT NOT NULL DEFAULT 0,
    CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    LastUpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
