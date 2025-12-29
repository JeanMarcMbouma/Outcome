# BbQ.Events.SqlServer

SQL Server implementation of `IProjectionCheckpointStore` for BbQ.Events projections.

This package provides a production-ready, durable checkpoint persistence mechanism for BbQ.Events projections using SQL Server as the storage backend.

## Features

- ✅ **Durable Persistence**: Checkpoints survive application restarts
- ✅ **Atomic Operations**: MERGE-based upserts prevent race conditions
- ✅ **Thread-Safe**: Safe for parallel processing and multiple instances
- ✅ **Minimal Dependencies**: Uses raw ADO.NET for performance
- ✅ **Future-Ready**: Schema supports partitioned projections

## Installation

```bash
dotnet add package BbQ.Events.SqlServer
```

## Database Schema

Create the checkpoint table in your SQL Server database:

```sql
CREATE TABLE BbQ_ProjectionCheckpoints (
    ProjectionName NVARCHAR(200) NOT NULL,
    PartitionKey NVARCHAR(200) NULL,
    Position BIGINT NOT NULL,
    LastUpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    PRIMARY KEY (ProjectionName, PartitionKey)
);
```

**Note**: The `PartitionKey` column is nullable and defaults to `NULL` for non-partitioned projections. SQL Server allows nullable columns in composite primary keys. Due to how NULL values work in unique constraints, only one row with a NULL `PartitionKey` can exist per `ProjectionName`, which is the desired behavior for non-partitioned projections. This schema design enables future support for partitioned projection features.

## Usage

### Basic Setup

```csharp
using BbQ.Events.DependencyInjection;
using BbQ.Events.SqlServer;

var services = new ServiceCollection();

// Register event bus and projections
services.AddInMemoryEventBus();
services.AddProjection<UserProfileProjection>();

// Register SQL Server checkpoint store
// For development with self-signed certificates, add: TrustServerCertificate=true
services.UseSqlServerCheckpoints(
    "Server=localhost;Database=MyApp;Integrated Security=true");

// Register projection engine
services.AddProjectionEngine();

var provider = services.BuildServiceProvider();
```

### Running Projections

```csharp
// Get the projection engine
var engine = provider.GetRequiredService<IProjectionEngine>();

// Run projections (blocks until cancelled)
await engine.RunAsync(cancellationToken);
```

### Connection String Configuration

Recommended: Store connection strings in configuration:

```csharp
var connectionString = builder.Configuration.GetConnectionString("ProjectionCheckpoints");
services.UseSqlServerCheckpoints(connectionString);
```

**appsettings.json:**
```json
{
  "ConnectionStrings": {
    "ProjectionCheckpoints": "Server=localhost;Database=MyApp;Integrated Security=true"
  }
}
```

**Note**: For development environments with self-signed certificates, you may need to add `TrustServerCertificate=true` to the connection string. However, this should not be used in production as it disables TLS certificate validation and can allow man-in-the-middle attacks.

## Concurrency and Idempotency

### Atomic Updates

The implementation uses SQL Server's `MERGE` statement for atomic upsert operations:

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

The checkpoint store is safe for parallel processing scenarios:

- ✅ Multiple projection instances can run concurrently
- ✅ Different projections maintain independent checkpoints
- ✅ Connection pooling handles concurrent database access
- ✅ Proper async/await patterns for scalability

## Performance Considerations

### Connection Pooling

The implementation relies on ADO.NET's built-in connection pooling:

- Each operation opens and closes a connection
- Connection pooling handles reuse automatically
- No manual connection management required

### Recommended Indexes

The primary key provides optimal query performance:

```sql
PRIMARY KEY (ProjectionName, PartitionKey)
```

For queries filtering by `LastUpdatedUtc`, consider adding:

```sql
CREATE INDEX IX_BbQ_ProjectionCheckpoints_LastUpdatedUtc 
ON BbQ_ProjectionCheckpoints(LastUpdatedUtc);
```

## Migration from In-Memory Store

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

3. **Verify table exists**:
   ```sql
   SELECT * FROM INFORMATION_SCHEMA.TABLES 
   WHERE TABLE_NAME = 'BbQ_ProjectionCheckpoints';
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

The schema is designed to support future features:

- **Partitioned Projections**: The `PartitionKey` column enables per-partition checkpointing
- **Partition Metadata**: Additional columns can be added without breaking changes
- **Monitoring**: The `LastUpdatedUtc` column enables projection health monitoring

## License

MIT License - see LICENSE.txt for details

## Contributing

Contributions are welcome! Please open an issue or pull request at:
https://github.com/JeanMarcMbouma/Outcome
