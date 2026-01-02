# BbQ.Events.SqlServer

SQL Server implementation for BbQ.Events, providing both event store and checkpoint persistence.

This package provides production-ready, durable implementations for:
- **Event Store**: Full event sourcing with IEventStore for SQL Server
- **Checkpoint Store**: Projection checkpoint persistence with IProjectionCheckpointStore

## Features

- ✅ **Durable Event Store**: Sequential event persistence with stream isolation
- ✅ **Atomic Operations**: MERGE-based upserts prevent race conditions
- ✅ **Thread-Safe**: Safe for parallel processing and multiple instances
- ✅ **Checkpoint Persistence**: Durable projection checkpoints
- ✅ **Minimal Dependencies**: Uses raw ADO.NET for performance
- ✅ **JSON Serialization**: Flexible event data serialization
- ✅ **Feature-Based Architecture**: Organized by capability (Events, Checkpointing, Schema, etc.)

## Installation

```bash
dotnet add package BbQ.Events.SqlServer
```

## Database Schema

The package includes SQL schema files in the `Schema/` folder. Run these scripts to set up your database:

### 1. Events Table (for Event Store)

```sql
-- See Schema/CreateEventsTable.sql for full script
CREATE TABLE BbQ_Events (
    EventId BIGINT IDENTITY(1,1) PRIMARY KEY,
    StreamName NVARCHAR(200) NOT NULL,
    Position BIGINT NOT NULL,
    EventType NVARCHAR(500) NOT NULL,
    EventData NVARCHAR(MAX) NOT NULL,
    Metadata NVARCHAR(MAX) NULL,
    CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_BbQ_Events_Stream_Position UNIQUE (StreamName, Position)
);
```

### 2. Streams Table (for Event Store)

```sql
-- See Schema/CreateStreamsTable.sql for full script
CREATE TABLE BbQ_Streams (
    StreamName NVARCHAR(200) PRIMARY KEY,
    CurrentPosition BIGINT NOT NULL DEFAULT -1,
    Version INT NOT NULL DEFAULT 0,
    CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    LastUpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
```

### 3. Checkpoints Table (for Projection Checkpoints)

```sql
-- See Schema/CreateCheckpointTable.sql for full script
CREATE TABLE BbQ_ProjectionCheckpoints (
    ProjectionName NVARCHAR(200) NOT NULL,
    PartitionKey NVARCHAR(200) NULL,
    Position BIGINT NOT NULL,
    LastUpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    PRIMARY KEY (ProjectionName, PartitionKey)
);
```

**Note**: The `PartitionKey` column is nullable and defaults to `NULL` for non-partitioned projections. SQL Server allows nullable columns in composite primary keys. Due to how NULL values work in unique constraints, only one row with a NULL `PartitionKey` can exist per `ProjectionName`, which is the desired behavior for non-partitioned projections.

## Usage

### Event Store

Use the SQL Server event store for durable event persistence:

```csharp
using BbQ.Events.SqlServer.Configuration;

var services = new ServiceCollection();

// Register SQL Server event store
services.UseSqlServerEventStore("Server=localhost;Database=MyApp;Integrated Security=true");

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
services.UseSqlServerEventStore(options =>
{
    options.ConnectionString = "Server=localhost;Database=MyApp;Integrated Security=true";
    options.IncludeMetadata = true;        // Include metadata (timestamp, server, etc.)
    options.JsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
});
```

### Checkpoint Store (for Projections)

Use SQL Server checkpoint store for durable projection checkpoints:

```csharp
using BbQ.Events.Configuration;
using BbQ.Events.SqlServer.Configuration;

var services = new ServiceCollection();

// Register event bus and projections
services.AddInMemoryEventBus();
services.AddProjection<UserProfileProjection>();

// Register SQL Server checkpoint store
services.UseSqlServerCheckpoints(
    "Server=localhost;Database=MyApp;Integrated Security=true");

// Register projection engine
services.AddProjectionEngine();

var provider = services.BuildServiceProvider();

// Get the projection engine
var engine = provider.GetRequiredService<IProjectionEngine>();

// Run projections (blocks until cancelled)
await engine.RunAsync(cancellationToken);
```

### Complete Example with Both Event Store and Checkpoints

```csharp
using BbQ.Events.Configuration;
using BbQ.Events.SqlServer.Configuration;

var services = new ServiceCollection();

// Register SQL Server event store for event sourcing
services.UseSqlServerEventStore("Server=localhost;Database=MyApp;Integrated Security=true");

// Register event bus for pub/sub
services.AddInMemoryEventBus();

// Register projections
services.AddProjection<UserProfileProjection>();

// Register SQL Server checkpoint store for projections
services.UseSqlServerCheckpoints("Server=localhost;Database=MyApp;Integrated Security=true");

// Register projection engine
services.AddProjectionEngine();

var provider = services.BuildServiceProvider();
```

### Connection String Configuration

Recommended: Store connection strings in configuration:

```csharp
var eventStoreConnection = builder.Configuration.GetConnectionString("EventStore");
var checkpointConnection = builder.Configuration.GetConnectionString("Checkpoints");

services.UseSqlServerEventStore(eventStoreConnection);
services.UseSqlServerCheckpoints(checkpointConnection);
```

**appsettings.json:**
```json
{
  "ConnectionStrings": {
    "EventStore": "Server=localhost;Database=MyApp;Integrated Security=true",
    "Checkpoints": "Server=localhost;Database=MyApp;Integrated Security=true"
  }
}
```

**Note**: For development environments with self-signed certificates, you may need to add `TrustServerCertificate=true` to the connection string. However, this should not be used in production as it disables TLS certificate validation and can allow man-in-the-middle attacks.

## Architecture

The package follows a feature-based folder structure:

```
BbQ.Events.SqlServer/
  Events/                    # Event store implementation
    SqlServerEventStore.cs
    SqlServerEventStoreOptions.cs
  
  Checkpointing/            # Projection checkpoint store
    SqlServerProjectionCheckpointStore.cs
  
  Schema/                   # SQL schema scripts
    CreateEventsTable.sql
    CreateStreamsTable.sql
    CreateCheckpointTable.sql
  
  Configuration/            # DI extensions
    ServiceCollectionExtensions.cs
  
  Internal/                 # Internal helpers (not public API)
    SqlHelpers.cs
    SqlConstants.cs
```

This structure:
- Aligns with the BbQ.Events core library architecture
- Makes it easy to find related functionality
- Separates concerns cleanly
- Provides clear separation between public API and internal implementation

## Concurrency and Idempotency

### Event Store Atomicity

The event store uses SQL Server's transaction support to ensure:
- **Sequential positions**: Events in a stream have guaranteed sequential positions
- **Stream isolation**: Different streams maintain independent positions
- **Concurrent appends**: Multiple processes can append to different streams simultaneously
- **Optimistic concurrency**: Stream versioning prevents conflicts

### Checkpoint Atomicity

The checkpoint store uses SQL Server's `MERGE` statement for atomic upsert operations:

```sql
MERGE BbQ_ProjectionCheckpoints AS target
USING (SELECT @ProjectionName, @PartitionKey) AS source
ON target.ProjectionName = source.ProjectionName 
   AND target.PartitionKey IS NULL
WHEN MATCHED THEN UPDATE SET Position = @Position
WHEN NOT MATCHED THEN INSERT (...)
```

This ensures that:
- **Concurrent writes are safe**: Multiple processes can write checkpoints simultaneously
- **Last write wins**: The most recent checkpoint value is always persisted
- **No lost updates**: Atomic operations prevent race conditions

### Parallel Processing

Both implementations are safe for parallel processing scenarios:

- ✅ Multiple instances can run concurrently
- ✅ Different streams/projections maintain independent state
- ✅ Connection pooling handles concurrent database access
- ✅ Proper async/await patterns for scalability

## Performance Considerations

### Connection Pooling

Both implementations rely on ADO.NET's built-in connection pooling:

- Each operation opens and closes a connection
- Connection pooling handles reuse automatically
- No manual connection management required

### Recommended Indexes

All schema files include recommended indexes. The primary indexes are:

**Events Table:**
```sql
-- Unique constraint ensures position uniqueness within a stream
CONSTRAINT UQ_BbQ_Events_Stream_Position UNIQUE (StreamName, Position)

-- Optimized for reading events from a stream
CREATE INDEX IX_BbQ_Events_StreamName_Position ON BbQ_Events(StreamName, Position);
```

**Checkpoints Table:**
```sql
-- Primary key for fast checkpoint lookups
PRIMARY KEY (ProjectionName, PartitionKey)
```

## Migration Guides

### From In-Memory Event Store to SQL Server

Replace the in-memory event store with SQL Server:

**Before:**
```csharp
services.AddSingleton<IEventStore, InMemoryEventStore>();
```

**After:**
```csharp
services.UseSqlServerEventStore(connectionString);
```

All event store client code remains unchanged.

### From In-Memory Checkpoint Store to SQL Server

Replace the in-memory checkpoint store with SQL Server:

**Before:**
```csharp
services.AddProjectionEngine(); // Uses InMemoryProjectionCheckpointStore
```

**After:**
```csharp
services.UseSqlServerCheckpoints(connectionString);
services.AddProjectionEngine();
```

All existing projection code remains unchanged.

## Troubleshooting

### Connection Issues

If you encounter connection errors:

1. **Verify SQL Server is accessible**:
   ```bash
   sqlcmd -S localhost -d MyApp -E
   ```

2. **Check connection string**: Ensure it's valid and includes necessary options like `TrustServerCertificate=true` for dev environments

3. **Verify tables exist**:
   ```sql
   SELECT * FROM INFORMATION_SCHEMA.TABLES 
   WHERE TABLE_NAME LIKE 'BbQ_%';
   ```

### Events Not Persisting

If events aren't being saved:

1. **Verify tables exist**:
   ```sql
   SELECT * FROM INFORMATION_SCHEMA.TABLES 
   WHERE TABLE_NAME IN ('BbQ_Events', 'BbQ_Streams');
   ```

2. **Check for errors**: Enable logging to see any exceptions

3. **Query events directly**:
   ```sql
   SELECT * FROM BbQ_Events;
   SELECT * FROM BbQ_Streams;
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
   SELECT * FROM BbQ_ProjectionCheckpoints;
   ```

## Future Enhancements

The schema and architecture are designed to support future features:

### Event Store
- **Event versioning**: Track event schema versions for migrations
- **Snapshots**: Optimize stream replay with periodic snapshots
- **Global ordering**: Cross-stream event ordering via EventId
- **Event metadata**: Custom metadata for correlation and causation tracking

### Checkpoint Store
- **Partitioned Projections**: The `PartitionKey` column enables per-partition checkpointing
- **Partition Metadata**: Additional columns can be added without breaking changes
- **Monitoring**: The `LastUpdatedUtc` column enables projection health monitoring
- **Checkpoint batching**: Batch checkpoint updates for improved throughput

### General
- **Replay features**: Integration with projection replay and rebuilding
- **Parallelism**: Parallel projection processing support
- **Batching**: Batch event reads and writes for improved performance

## License

MIT License - see LICENSE.txt for details

## Contributing

Contributions are welcome! Please open an issue or pull request at:
https://github.com/JeanMarcMbouma/Outcome
