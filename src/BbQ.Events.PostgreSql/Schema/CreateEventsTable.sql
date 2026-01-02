-- BbQ Events Table
-- Stores individual events with their metadata
-- Each event belongs to a stream and has a sequential position

CREATE TABLE bbq_events (
    -- Event identity
    event_id BIGSERIAL PRIMARY KEY,
    stream_name VARCHAR(200) NOT NULL,
    position BIGINT NOT NULL,
    
    -- Event metadata
    event_type VARCHAR(500) NOT NULL,
    event_data TEXT NOT NULL,
    metadata TEXT NULL,
    
    -- Timestamps
    created_utc TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
    
    -- Ensure position uniqueness within a stream
    -- This constraint also creates an index for efficient stream reads
    CONSTRAINT uq_bbq_events_stream_position UNIQUE (stream_name, position)
);

-- Create index for efficient stream reads
CREATE INDEX ix_bbq_events_stream_name_position ON bbq_events(stream_name, position);
