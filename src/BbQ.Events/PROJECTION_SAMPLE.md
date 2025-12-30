# Projection Support Sample

This sample demonstrates how to use the projection feature in BbQ.Events to build read models from event streams.

## Overview

Projections enable you to transform events into queryable state. This sample shows:

1. **Simple Projection** - A projection that handles a single event type
2. **Multi-Event Projection** - A projection that handles multiple related events
3. **Partitioned Projection** - A projection with partitioning for parallel processing

## Events

```csharp
// Domain events
public record UserRegistered(Guid UserId, string Email, string Name, DateTime RegisteredAt);
public record UserProfileUpdated(Guid UserId, string Name, string Bio);
public record UserLoginOccurred(Guid UserId, DateTime LoginAt, string IpAddress);
```

## Projections

### 1. User Profile Read Model

This projection maintains an up-to-date view of user profiles by handling registration and update events:

```csharp
[Projection]
public class UserProfileProjection :
    IProjectionHandler<UserRegistered>,
    IProjectionHandler<UserProfileUpdated>
{
    private readonly IUserProfileRepository _repository;
    
    public UserProfileProjection(IUserProfileRepository repository)
    {
        _repository = repository;
    }
    
    public async ValueTask ProjectAsync(UserRegistered evt, CancellationToken ct)
    {
        var profile = new UserProfile
        {
            UserId = evt.UserId,
            Email = evt.Email,
            Name = evt.Name,
            RegisteredAt = evt.RegisteredAt
        };
        
        await _repository.UpsertAsync(profile, ct);
    }
    
    public async ValueTask ProjectAsync(UserProfileUpdated evt, CancellationToken ct)
    {
        var profile = await _repository.GetByIdAsync(evt.UserId, ct);
        if (profile != null)
        {
            profile.Name = evt.Name;
            profile.Bio = evt.Bio;
            await _repository.UpsertAsync(profile, ct);
        }
    }
}
```

### 2. User Login Statistics (Partitioned)

This projection tracks login statistics and uses partition keys for future parallelization:

```csharp
[Projection]
public class UserLoginStatisticsProjection : IPartitionedProjectionHandler<UserLoginOccurred>
{
    private readonly ILoginStatsRepository _repository;
    
    public UserLoginStatisticsProjection(ILoginStatsRepository repository)
    {
        _repository = repository;
    }
    
    public string GetPartitionKey(UserLoginOccurred evt)
    {
        // Partition by user ID - can be used for parallel processing in custom engines
        return evt.UserId.ToString();
    }
    
    public async ValueTask ProjectAsync(UserLoginOccurred evt, CancellationToken ct)
    {
        var stats = await _repository.GetByUserIdAsync(evt.UserId, ct)
            ?? new UserLoginStats { UserId = evt.UserId };
        
        stats.LoginCount++;
        stats.LastLoginAt = evt.LoginAt;
        stats.LastIpAddress = evt.IpAddress;
        
        await _repository.UpsertAsync(stats, ct);
    }
}
```

**Note:** The default projection engine processes events sequentially. To leverage partition keys for parallel processing, implement a custom `IProjectionEngine`.

## Registration

### Manual Registration

```csharp
var services = new ServiceCollection();

// 1. Register event bus
services.AddInMemoryEventBus();

// 2. Register projections manually
services.AddProjection<UserProfileProjection>();
services.AddProjection<UserLoginStatisticsProjection>();

// 3. Register projection engine
services.AddProjectionEngine();

// 4. Register repositories
services.AddScoped<IUserProfileRepository, UserProfileRepository>();
services.AddScoped<ILoginStatsRepository, LoginStatsRepository>();
```

### Assembly Scanning

```csharp
var services = new ServiceCollection();

// 1. Register event bus
services.AddInMemoryEventBus();

// 2. Scan assembly for projections with [Projection] attribute
services.AddProjectionsFromAssembly(typeof(Program).Assembly);

// 3. Register projection engine
services.AddProjectionEngine();

// 4. Register repositories
services.AddScoped<IUserProfileRepository, UserProfileRepository>();
services.AddScoped<ILoginStatsRepository, LoginStatsRepository>();
```

## Projection Startup Modes

Projections can start in different modes depending on your deployment needs:

```csharp
// Resume mode (default): Continue from last checkpoint
services.AddProjection<UserProfileProjection>(options => 
{
    options.StartupMode = ProjectionStartupMode.Resume;
});

// Replay mode: Rebuild from scratch, ignoring checkpoint
services.AddProjection<UserProfileProjection>(options => 
{
    options.StartupMode = ProjectionStartupMode.Replay;
});

// CatchUp mode: Fast-forward to near-real-time
services.AddProjection<UserProfileProjection>(options => 
{
    options.StartupMode = ProjectionStartupMode.CatchUp;
});

// LiveOnly mode: Process only new events
services.AddProjection<UserProfileProjection>(options => 
{
    options.StartupMode = ProjectionStartupMode.LiveOnly;
});
```

You can also configure startup modes via the `[Projection]` attribute:

```csharp
// Replay mode on every startup (useful for development)
[Projection(StartupMode = ProjectionStartupMode.Replay)]
public class UserProfileProjection : IProjectionHandler<UserRegistered>
{
    // ...
}

// Live-only mode for real-time analytics
[Projection(StartupMode = ProjectionStartupMode.LiveOnly)]
public class RealtimeAnalyticsProjection : IProjectionHandler<UserActivity>
{
    // ...
}
```

**Startup Mode Behaviors:**

- **Resume** (default): Loads the last checkpoint and continues from where it left off. This is the standard behavior for production projections.

- **Replay**: Ignores any existing checkpoint and rebuilds the projection from the beginning of the event stream. Useful for:
  - Recovering from corrupted projection state
  - Rebuilding after schema changes
  - Testing projection logic with historical data

- **CatchUp**: Starts from a recent position near the current time, then processes events normally. Useful for:
  - New projections that don't need full historical data
  - Getting projections up to speed quickly

- **LiveOnly**: Processes only new events from the current moment, ignoring all historical events. Useful for:
  - Real-time analytics and monitoring
  - Projections that only track future activity
  - Development and testing scenarios

## Running the Projection Engine

### As a Background Service

```csharp
public class ProjectionHostedService : BackgroundService
{
    private readonly IProjectionEngine _engine;
    private readonly ILogger<ProjectionHostedService> _logger;
    
    public ProjectionHostedService(
        IProjectionEngine engine,
        ILogger<ProjectionHostedService> logger)
    {
        _engine = engine;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting projection engine");
        
        try
        {
            await _engine.RunAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Projection engine failed");
            throw;
        }
        
        _logger.LogInformation("Projection engine stopped");
    }
}

// Register the hosted service
services.AddHostedService<ProjectionHostedService>();
```

### Manually in Console App

```csharp
public class Program
{
    public static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddInMemoryEventBus();
        services.AddProjectionsFromAssembly(typeof(Program).Assembly);
        services.AddProjectionEngine();
        
        var provider = services.BuildServiceProvider();
        
        // Start projection engine
        var engine = provider.GetRequiredService<IProjectionEngine>();
        var cts = new CancellationTokenSource();
        
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        
        Console.WriteLine("Projection engine starting. Press Ctrl+C to stop.");
        await engine.RunAsync(cts.Token);
        Console.WriteLine("Projection engine stopped.");
    }
}
```

## Publishing Events

```csharp
public class UserService
{
    private readonly IEventPublisher _eventPublisher;
    
    public UserService(IEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher;
    }
    
    public async Task RegisterUserAsync(string email, string name)
    {
        var userId = Guid.NewGuid();
        
        // Publish event - projections will automatically process it
        await _eventPublisher.Publish(new UserRegistered(
            userId, 
            email, 
            name, 
            DateTime.UtcNow
        ));
    }
}
```

## Best Practices

1. **Idempotency**: Design projections to be idempotent so that processing the same event multiple times produces the same result. While the default engine processes live events and doesn't replay them, idempotency keeps projections safe for scenarios like republished events or future replay support.

2. **Determinism**: Projection logic should be deterministic - the same event should always produce the same projection.

3. **No Side Effects**: Projections should only update read models, not trigger external actions like sending emails.

4. **Partition Keys**: For `IPartitionedProjectionHandler`, choose partition keys that group related events together. These can be leveraged by custom engine implementations for parallel processing.

5. **Checkpoint Storage**: The checkpoint store infrastructure is provided via `IProjectionCheckpointStore`. Implement custom checkpointing logic in your projection engine or handlers as needed for your use case.

## Advanced: Custom Checkpoint Store (Infrastructure)

The following example shows how a durable checkpoint store can be implemented. Note that the default projection engine doesn't automatically use checkpoints - you would integrate this in a custom engine implementation or manually in your projections.

Example using SQL with Dapper (requires `Dapper` NuGet package):

```csharp
using Dapper; // Install Dapper NuGet package
using System.Data;

public class SqlProjectionCheckpointStore : IProjectionCheckpointStore
{
    private readonly IDbConnection _connection;
    
    public SqlProjectionCheckpointStore(IDbConnection connection)
    {
        _connection = connection;
    }
    
    public async ValueTask<long?> GetCheckpointAsync(string projectionName, CancellationToken ct)
    {
        var checkpoint = await _connection.QuerySingleOrDefaultAsync<long?>(
            "SELECT Checkpoint FROM ProjectionCheckpoints WHERE ProjectionName = @Name",
            new { Name = projectionName });
        return checkpoint;
    }
    
    public async ValueTask SaveCheckpointAsync(string projectionName, long checkpoint, CancellationToken ct)
    {
        // PostgreSQL example - adapt for your database
        await _connection.ExecuteAsync(@"
            INSERT INTO ProjectionCheckpoints (ProjectionName, Checkpoint, UpdatedAt)
            VALUES (@Name, @Checkpoint, @UpdatedAt)
            ON CONFLICT (ProjectionName) 
            DO UPDATE SET Checkpoint = @Checkpoint, UpdatedAt = @UpdatedAt",
            new { Name = projectionName, Checkpoint = checkpoint, UpdatedAt = DateTime.UtcNow });
            
        // SQL Server alternative:
        // MERGE INTO ProjectionCheckpoints AS target
        // USING (SELECT @Name AS ProjectionName) AS source
        // ON target.ProjectionName = source.ProjectionName
        // WHEN MATCHED THEN 
        //     UPDATE SET Checkpoint = @Checkpoint, UpdatedAt = @UpdatedAt
        // WHEN NOT MATCHED THEN
        //     INSERT (ProjectionName, Checkpoint, UpdatedAt)
        //     VALUES (@Name, @Checkpoint, @UpdatedAt);
    }
    
    public async ValueTask ResetCheckpointAsync(string projectionName, CancellationToken ct)
    {
        await _connection.ExecuteAsync(
            "DELETE FROM ProjectionCheckpoints WHERE ProjectionName = @Name",
            new { Name = projectionName });
    }
}

// Register custom checkpoint store
services.AddSingleton<IProjectionCheckpointStore, SqlProjectionCheckpointStore>();
services.AddProjectionEngine();
```

**Database Schema:**

```sql
-- PostgreSQL
CREATE TABLE ProjectionCheckpoints (
    ProjectionName VARCHAR(255) PRIMARY KEY,
    Checkpoint BIGINT NOT NULL,
    UpdatedAt TIMESTAMP NOT NULL
);

-- SQL Server
CREATE TABLE ProjectionCheckpoints (
    ProjectionName NVARCHAR(255) PRIMARY KEY,
    Checkpoint BIGINT NOT NULL,
    UpdatedAt DATETIME2 NOT NULL
);
```
