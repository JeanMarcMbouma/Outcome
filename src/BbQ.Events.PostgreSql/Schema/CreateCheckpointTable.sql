-- BbQ Projection Checkpoints Table
-- Stores checkpoint positions for projections
-- Supports both simple and partitioned projections

CREATE TABLE bbq_projection_checkpoints (
    projection_name TEXT NOT NULL,
    partition_key TEXT NULL,
    position BIGINT NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (projection_name, partition_key)
);

-- Index for time-based queries (projection health monitoring)
CREATE INDEX idx_bbq_projection_checkpoints_updated_at 
ON bbq_projection_checkpoints(updated_at);

-- Index for querying all checkpoints of a projection
CREATE INDEX idx_bbq_projection_checkpoints_projection_name 
ON bbq_projection_checkpoints(projection_name);
