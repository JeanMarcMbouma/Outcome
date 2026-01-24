# BbQ.Events.PostgreSql

PostgreSQL implementation for BbQ.Events, providing both event store and checkpoint persistence.

This package provides production-ready, durable implementations for:
- **Event Store**: Full event sourcing with IEventStore for PostgreSQL
- **Checkpoint Store**: Projection checkpoint persistence with IProjectionCheckpointStore

## Features

- ✅ **Durable Event Store**: Sequential event persistence with stream isolation
- ✅ **Durable Checkpoint Store**: Persistent projection checkpoints
- ✅ **Atomic Operations**: INSERT ... ON CONFLICT upserts prevent race conditions
- ✅ **Thread-safe**: Safe for parallel processing and multiple instances
- ✅ **Automatic Schema Creation**: Optional schema initialization on startup
- ✅ **Minimal Dependencies**: Uses Npgsql (PostgreSQL ADO.NET provider) for performance
- ✅ **JSON Serialization**: Flexible event data serialization
- ✅ **Partitioned Projections**: Support for partition-based checkpointing (schema-ready)
- ✅ **Feature-Based Architecture**: Organized by capability (Events, Checkpointing, Schema, Configuration)

## Installation

```bash
dotnet add package BbQ.Events.PostgreSql
```

## Database Schema

The package includes SQL schema files in the `Schema/` folder.

### Automatic Schema Creation (Recommended)

The simplest way to set up the database schema is to enable automatic schema creation:

```csharp
services.UsePostgreSqlEventStore(options =>
{
    options.ConnectionString = "Host=localhost;Database=myapp;Username=myuser;Password=mypass";
    options.AutoCreateSchema = true;  // Automatically create tables if they don't exist
});
```

When `AutoCreateSchema` is enabled:
- ✅ Tables are created automatically on application startup if they don't exist
- ✅ The schema creation is idempotent (safe to run multiple times)
- ✅ Existing tables are not modified
- ✅ Uses the embedded SQL scripts to ensure consistency

**Note**: For production environments, you may prefer to run the SQL scripts manually for more control. Set `AutoCreateSchema = false` (the default) and execute the scripts during deployment.

### Manual Schema Creation

Alternatively, you can manually run the schema scripts:

### Events Table (for Event Store)

```sql
-- See Schema/CreateEventsTable.sql for full script
CREATE TABLE bbq_events (
    event_id BIGSERIAL PRIMARY KEY,
    stream_name VARCHAR(200) NOT NULL,
    position BIGINT NOT NULL,
    event_type VARCHAR(500) NOT NULL,
    event_data TEXT NOT NULL,
    metadata TEXT NULL,
    created_utc TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
    CONSTRAINT uq_bbq_events_stream_position UNIQUE (stream_name, position)
);
```

### Streams Table (for Event Store)

```sql
-- See Schema/CreateStreamsTable.sql for full script
CREATE TABLE bbq_streams (
    stream_name VARCHAR(200) PRIMARY KEY,
    current_position BIGINT NOT NULL DEFAULT -1,
    version INT NOT NULL DEFAULT 0,
    created_utc TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
    last_updated_utc TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')
);
```

### Checkpoints Table (for Projection Checkpoints)

```sql
-- See Schema/CreateCheckpointTable.sql for full script
CREATE TABLE bbq_projection_checkpoints (
    projection_name TEXT NOT NULL,
    partition_key TEXT NULL DEFAULT NULL,
    position BIGINT NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_bbq_projection_checkpoints PRIMARY KEY (projection_name, partition_key) NULLS NOT DISTINCT
);
```

**Note**: The `partition_key` column is nullable and defaults to `NULL` for non-partitioned projections. PostgreSQL allows nullable columns in composite primary keys with the `NULLS NOT DISTINCT` clause (PostgreSQL 15+). This ensures only one row with a NULL `partition_key` can exist per `projection_name`, which is the desired behavior for non-partitioned projections.

### Explicit Schema Initialization

You can also manually trigger schema initialization at any time:

```csharp
var eventStore = provider.GetRequiredService<IEventStore>();

// Ensure the schema exists (creates tables if they don't exist)
await eventStore.EnsureSchemaAsync();
```

This is useful for:
- Testing environments where you want to set up the database on-demand
- Initialization logic in application startup
- Migration scenarios where you're adding the event store to an existing application

## Usage

### Event Store

Use the PostgreSQL event store for durable event persistence:

```csharp
using BbQ.Events.PostgreSql.Configuration;

var services = new ServiceCollection();

// Register PostgreSQL event store
services.UsePostgreSqlEventStore("Host=localhost;Database=myapp;Username=myuser;Password=mypass");

var provider = services.BuildServiceProvider();
var eventStore = provider.GetRequiredService<IEventStore>();

// Append events to a stream
var userId = Guid.NewGuid();
await eventStore.AppendAsync("users", new UserCreated(userId, "Alice", "alice@example.com"));
await eventStore.AppendAsync("users", new UserUpdated(userId, "Alice Smith"));

// Read events from a stream
await foreach (var storedEvent in eventStore.ReadAsync<UserCreated>("users"))
{
    Console.WriteLine($"Position {storedEvent.Position}: {storedEvent.Event.Name}");
}

// Get current stream position
var position = await eventStore.GetStreamPositionAsync("users");
Console.WriteLine($"Stream at position: {position}");
```

### Event Store with Options

Configure advanced options:

```csharp
services.UsePostgreSqlEventStore(options =>
{
    options.ConnectionString = "Host=localhost;Database=myapp;Username=myuser;Password=mypass";
    options.IncludeMetadata = true;        // Include metadata (timestamp, server, etc.)
    options.AutoCreateSchema = true;       // Automatically create schema if missing
    options.JsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
});
```

### Checkpoint Store (for Projections)

Use PostgreSQL checkpoint store for durable projection checkpoints:

```csharp
using BbQ.Events.Configuration;
using BbQ.Events.PostgreSql.Configuration;

var services = new ServiceCollection();

// Register event bus and projections
services.AddInMemoryEventBus();
services.AddProjection<UserProfileProjection>();

// Register PostgreSQL checkpoint store
services.UsePostgreSqlCheckpoints(
    "Host=localhost;Database=myapp;Username=myuser;Password=mypass");

// Register projection engine
services.AddProjectionEngine();

var provider = services.BuildServiceProvider();

// Get the projection engine
var engine = provider.GetRequiredService<IProjectionEngine>();

// Run projections (blocks until cancelled)
await engine.RunAsync(cancellationToken);
```

### Complete Example with Event Store and Checkpoints

```csharp
using BbQ.Events.Configuration;
using BbQ.Events.PostgreSql.Configuration;

var services = new ServiceCollection();

// Register PostgreSQL event store for event sourcing
services.UsePostgreSqlEventStore(options =>
{
    options.ConnectionString = "Host=localhost;Database=myapp;Username=myuser;Password=mypass";
    options.AutoCreateSchema = true;  // Automatically create schema
});

// Register event bus for pub/sub
services.AddInMemoryEventBus();

// Register projections
services.AddProjection<UserProfileProjection>();

// Register PostgreSQL checkpoint store for projections
services.UsePostgreSqlCheckpoints("Host=localhost;Database=myapp;Username=myuser;Password=mypass");

// Register projection engine
services.AddProjectionEngine();

var provider = services.BuildServiceProvider();
```

### Connection String Configuration

Recommended: Store connection strings in configuration:

```csharp
var checkpointConnection = builder.Configuration.GetConnectionString("Checkpoints");

services.UsePostgreSqlCheckpoints(checkpointConnection);
```

**appsettings.json:**
```json
{
  "ConnectionStrings": {
    "Checkpoints": "Host=localhost;Database=myapp;Username=myuser;Password=mypass"
  }
}
```

### Connection String Examples

**Local Development:**
```
Host=localhost;Database=myapp;Username=myuser;Password=mypass
```

**Docker Container:**
```
Host=postgres-container;Port=5432;Database=myapp;Username=myuser;Password=mypass
```

**Production with SSL:**
```
Host=prod-server.example.com;Database=myapp;Username=myuser;Password=mypass;SSL Mode=Require
```

**Connection Pooling (default enabled):**
```
Host=localhost;Database=myapp;Username=myuser;Password=mypass;Maximum Pool Size=100
```

## Architecture

The package follows a feature-based folder structure:

```
BbQ.Events.PostgreSql/
  Checkpointing/            # Projection checkpoint store
    PostgreSqlProjectionCheckpointStore.cs
  
  Schema/                   # SQL schema scripts
    CreateCheckpointTable.sql
  
  Configuration/            # DI extensions
    ServiceCollectionExtensions.cs
```

This structure:
- Aligns with the BbQ.Events core library architecture
- Makes it easy to find related functionality
- Separates concerns cleanly
- Provides clear separation between public API and internal implementation

## Concurrency and Idempotency

### Checkpoint Atomicity

The checkpoint store uses PostgreSQL's `INSERT ... ON CONFLICT` statement for atomic upsert operations:

```sql
INSERT INTO bbq_projection_checkpoints (projection_name, partition_key, position, updated_at)
VALUES (@projection_name, NULL, @position, NOW())
ON CONFLICT (projection_name, partition_key)
DO UPDATE SET position = EXCLUDED.position, updated_at = NOW()
```

This ensures that:
- **Concurrent writes are safe**: Multiple processes can write checkpoints simultaneously
- **Last write wins**: The most recent checkpoint value is always persisted
- **No lost updates**: Atomic operations prevent race conditions

### Parallel Processing

The implementation is safe for parallel processing scenarios:

- ✅ Multiple instances can run concurrently
- ✅ Different projections maintain independent state
- ✅ Connection pooling handles concurrent database access
- ✅ Proper async/await patterns for scalability

## Performance Considerations

### Connection Pooling

The implementation relies on Npgsql's built-in connection pooling:

- Each operation opens and closes a connection
- Connection pooling handles reuse automatically
- No manual connection management required
- Default pool size is 100 connections (configurable in connection string)

### Recommended Indexes

The schema file includes recommended indexes:

**Checkpoints Table:**
```sql
-- Primary key for fast checkpoint lookups
PRIMARY KEY (projection_name, partition_key)

-- Index for projection health monitoring
CREATE INDEX idx_bbq_projection_checkpoints_updated_at 
ON bbq_projection_checkpoints(updated_at);

-- Index for querying all checkpoints of a projection
CREATE INDEX idx_bbq_projection_checkpoints_projection_name 
ON bbq_projection_checkpoints(projection_name);
```

## Migration Guides

### From In-Memory Checkpoint Store to PostgreSQL

Replace the in-memory checkpoint store with PostgreSQL:

**Before:**
```csharp
services.AddProjectionEngine(); // Uses InMemoryProjectionCheckpointStore
```

**After:**
```csharp
services.UsePostgreSqlCheckpoints(connectionString);
services.AddProjectionEngine();
```

All existing projection code remains unchanged.

### From SQL Server to PostgreSQL

Replace the SQL Server checkpoint store with PostgreSQL:

**Before:**
```csharp
services.UseSqlServerCheckpoints(connectionString);
services.AddProjectionEngine();
```

**After:**
```csharp
services.UsePostgreSqlCheckpoints(connectionString);
services.AddProjectionEngine();
```

Note: You'll need to migrate the checkpoint data from SQL Server to PostgreSQL. The schema is very similar.

## Troubleshooting

### Connection Issues

If you encounter connection errors:

1. **Verify PostgreSQL is accessible**:
   ```bash
   psql -h localhost -U myuser -d myapp
   ```

2. **Check connection string**: Ensure it's valid and includes necessary parameters

3. **Verify tables exist**:
   ```sql
   SELECT * FROM information_schema.tables 
   WHERE table_name = 'bbq_projection_checkpoints';
   ```

### Checkpoint Not Persisting

If checkpoints aren't being saved:

1. **Verify projection engine is running**:
   ```csharp
   await engine.RunAsync(cancellationToken);
   ```

2. **Check projection registration**:
   ```csharp
   services.AddProjection<YourProjection>();
   ```

3. **Query the checkpoint table directly**:
   ```sql
   SELECT * FROM bbq_projection_checkpoints;
   ```

### Permission Issues

If you get permission errors:

1. **Ensure the database user has necessary permissions**:
   ```sql
   GRANT SELECT, INSERT, UPDATE, DELETE ON bbq_projection_checkpoints TO myuser;
   ```

2. **Verify connection credentials** in your connection string

## Testing with Docker

For local development and testing, you can use Docker to run PostgreSQL:

```bash
docker run --name postgres-bbq \
  -e POSTGRES_PASSWORD=mypassword \
  -e POSTGRES_USER=myuser \
  -e POSTGRES_DB=myapp \
  -p 5432:5432 \
  -d postgres:16
```

Then use this connection string:
```
Host=localhost;Database=myapp;Username=myuser;Password=mypassword
```

## Future Enhancements

The schema and architecture are designed to support future features:

### Checkpoint Store
- **Partitioned Projections**: The `partition_key` column enables per-partition checkpointing
- **Partition Metadata**: Additional columns can be added without breaking changes
- **Monitoring**: The `updated_at` column enables projection health monitoring
- **Checkpoint batching**: Batch checkpoint updates for improved throughput

### General
- **Replay features**: Integration with projection replay and rebuilding
- **Parallelism**: Parallel projection processing support
- **Event Store**: PostgreSQL event store implementation (future package)

## License

MIT License - see LICENSE.txt for details

## Contributing

Contributions are welcome! Please open an issue or pull request at:
https://github.com/JeanMarcMbouma/Outcome
