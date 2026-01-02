-- BbQ Projection Checkpoints Table
-- Stores checkpoint positions for projections
-- Supports both simple and partitioned projections

CREATE TABLE bbq_projection_checkpoints (
    projection_name TEXT NOT NULL,
    partition_key TEXT NULL DEFAULT NULL,
    position BIGINT NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_bbq_projection_checkpoints PRIMARY KEY (projection_name, partition_key) NULLS NOT DISTINCT
);

-- Index for time-based queries (projection health monitoring)
CREATE INDEX idx_bbq_projection_checkpoints_updated_at 
ON bbq_projection_checkpoints(updated_at);

-- Index for querying all checkpoints of a projection
CREATE INDEX idx_bbq_projection_checkpoints_projection_name 
ON bbq_projection_checkpoints(projection_name);
