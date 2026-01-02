-- BbQ Projection Checkpoints Table
-- Stores checkpoint positions for projections
-- Supports both simple and partitioned projections

CREATE TABLE BbQ_ProjectionCheckpoints (
    ProjectionName NVARCHAR(200) NOT NULL,
    PartitionKey NVARCHAR(200) NULL,
    Position BIGINT NOT NULL,
    LastUpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    PRIMARY KEY (ProjectionName, PartitionKey)
);

-- Index for time-based queries (projection health monitoring)
CREATE INDEX IX_BbQ_ProjectionCheckpoints_LastUpdatedUtc 
ON BbQ_ProjectionCheckpoints(LastUpdatedUtc);

-- Index for querying all checkpoints of a projection
CREATE INDEX IX_BbQ_ProjectionCheckpoints_ProjectionName 
ON BbQ_ProjectionCheckpoints(ProjectionName);
