-- BbQ Streams Table
-- Stores stream metadata and current position
-- Used for position tracking

CREATE TABLE bbq_streams (
    stream_name VARCHAR(200) PRIMARY KEY,
    current_position BIGINT NOT NULL DEFAULT -1,
    version INT NOT NULL DEFAULT 0,
    created_utc TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
    last_updated_utc TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')
);
