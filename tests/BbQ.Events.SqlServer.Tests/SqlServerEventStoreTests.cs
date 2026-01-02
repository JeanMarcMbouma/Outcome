using BbQ.Events.SqlServer.Events;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace BbQ.Events.SqlServer.Tests;

/// <summary>
/// Integration tests for SqlServerEventStore.
/// 
/// These tests require a SQL Server instance (LocalDB or full SQL Server).
/// Set the connection string via environment variable: TEST_SQLSERVER_CONNECTION_STRING
/// 
/// If no connection string is provided, tests will be skipped.
/// </summary>
[TestFixture]
public class SqlServerEventStoreTests
{
    private string? _connectionString;
    private SqlServerEventStore? _store;
    private bool _canRunTests;

    // Test event types
    private record UserCreated(Guid UserId, string Name, string Email);
    private record UserUpdated(Guid UserId, string Name);
    private record OrderPlaced(Guid OrderId, decimal Amount);

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Try to get connection string from environment variable
        _connectionString = Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            // Try LocalDB as fallback
            _connectionString = @"Server=(localdb)\mssqllocaldb;Database=BbQEventsTest;Integrated Security=true";
        }

        try
        {
            // First, connect to master database to create the test database if needed
            var masterConnectionString = _connectionString.Replace("Database=BbQEventsTest", "Database=master");
            
            await using (var masterConnection = new SqlConnection(masterConnectionString))
            {
                await masterConnection.OpenAsync();

                // Create test database if it doesn't exist
                var createDbCommand = masterConnection.CreateCommand();
                createDbCommand.CommandText = @"
                    IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'BbQEventsTest')
                    BEGIN
                        CREATE DATABASE BbQEventsTest;
                    END";
                
                try
                {
                    await createDbCommand.ExecuteNonQueryAsync();
                }
                catch
                {
                    // Ignore errors - database might already exist or we might not have permission
                }
            }

            // Now connect to the test database to create the tables
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Create Streams table
            var createStreamsTableCommand = connection.CreateCommand();
            createStreamsTableCommand.CommandText = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BbQ_Streams')
                BEGIN
                    CREATE TABLE BbQ_Streams (
                        StreamName NVARCHAR(200) PRIMARY KEY,
                        CurrentPosition BIGINT NOT NULL DEFAULT -1,
                        Version INT NOT NULL DEFAULT 0,
                        CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                        LastUpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
                    );
                END";
            
            await createStreamsTableCommand.ExecuteNonQueryAsync();

            // Create Events table
            var createEventsTableCommand = connection.CreateCommand();
            createEventsTableCommand.CommandText = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BbQ_Events')
                BEGIN
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
                    
                    CREATE INDEX IX_BbQ_Events_StreamName_Position 
                    ON BbQ_Events(StreamName, Position);
                END";
            
            await createEventsTableCommand.ExecuteNonQueryAsync();

            _canRunTests = true;
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"SQL Server not available: {ex.Message}");
            TestContext.WriteLine("Tests will be skipped. To run tests, ensure SQL Server or LocalDB is available.");
            _canRunTests = false;
        }
    }

    [SetUp]
    public async Task SetUp()
    {
        if (!_canRunTests)
        {
            Assert.Ignore("SQL Server not available for testing");
            return;
        }

        var options = new SqlServerEventStoreOptions
        {
            ConnectionString = _connectionString!
        };
        _store = new SqlServerEventStore(options);

        // Clean up any existing test data
        await CleanupTestDataAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_canRunTests)
        {
            await CleanupTestDataAsync();
        }
    }

    private async Task CleanupTestDataAsync()
    {
        if (!_canRunTests || string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Delete test events
        await using var deleteEventsCommand = connection.CreateCommand();
        deleteEventsCommand.CommandText = "DELETE FROM BbQ_Events WHERE StreamName LIKE 'test-%'";
        await deleteEventsCommand.ExecuteNonQueryAsync();

        // Delete test streams
        await using var deleteStreamsCommand = connection.CreateCommand();
        deleteStreamsCommand.CommandText = "DELETE FROM BbQ_Streams WHERE StreamName LIKE 'test-%'";
        await deleteStreamsCommand.ExecuteNonQueryAsync();
    }

    [Test]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SqlServerEventStore(null!));
    }

    [Test]
    public void Constructor_WithEmptyConnectionString_ThrowsArgumentException()
    {
        // Arrange
        var options = new SqlServerEventStoreOptions { ConnectionString = "" };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SqlServerEventStore(options));
    }

    [Test]
    public async Task AppendAsync_FirstEvent_ReturnsPositionZero()
    {
        // Arrange
        var stream = "test-users-1";
        var @event = new UserCreated(Guid.NewGuid(), "Alice", "alice@example.com");

        // Act
        var position = await _store!.AppendAsync(stream, @event);

        // Assert
        Assert.That(position, Is.EqualTo(0));
    }

    [Test]
    public async Task AppendAsync_MultipleEvents_ReturnsSequentialPositions()
    {
        // Arrange
        var stream = "test-users-2";
        var event1 = new UserCreated(Guid.NewGuid(), "Alice", "alice@example.com");
        var event2 = new UserCreated(Guid.NewGuid(), "Bob", "bob@example.com");
        var event3 = new UserCreated(Guid.NewGuid(), "Charlie", "charlie@example.com");

        // Act
        var pos1 = await _store!.AppendAsync(stream, event1);
        var pos2 = await _store.AppendAsync(stream, event2);
        var pos3 = await _store.AppendAsync(stream, event3);

        // Assert
        Assert.That(pos1, Is.EqualTo(0));
        Assert.That(pos2, Is.EqualTo(1));
        Assert.That(pos3, Is.EqualTo(2));
    }

    [Test]
    public async Task AppendAsync_WithNullStream_ThrowsArgumentException()
    {
        // Arrange
        var @event = new UserCreated(Guid.NewGuid(), "Alice", "alice@example.com");

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _store!.AppendAsync<UserCreated>(null!, @event));
    }

    [Test]
    public async Task AppendAsync_WithNullEvent_ThrowsArgumentNullException()
    {
        // Arrange
        var stream = "test-users-3";

        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _store!.AppendAsync<UserCreated>(stream, null!));
    }

    [Test]
    public async Task ReadAsync_EmptyStream_ReturnsNoEvents()
    {
        // Arrange
        var stream = "test-users-4";
        var events = new List<UserCreated>();

        // Act
        await foreach (var storedEvent in _store!.ReadAsync<UserCreated>(stream))
        {
            events.Add(storedEvent.Event);
        }

        // Assert
        Assert.That(events, Is.Empty);
    }

    [Test]
    public async Task ReadAsync_WithEvents_ReturnsAllEvents()
    {
        // Arrange
        var stream = "test-users-5";
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        
        await _store!.AppendAsync(stream, new UserCreated(userId1, "Alice", "alice@example.com"));
        await _store.AppendAsync(stream, new UserCreated(userId2, "Bob", "bob@example.com"));

        // Act
        var events = new List<UserCreated>();
        await foreach (var storedEvent in _store.ReadAsync<UserCreated>(stream))
        {
            events.Add(storedEvent.Event);
        }

        // Assert
        Assert.That(events, Has.Count.EqualTo(2));
        Assert.That(events[0].UserId, Is.EqualTo(userId1));
        Assert.That(events[0].Name, Is.EqualTo("Alice"));
        Assert.That(events[1].UserId, Is.EqualTo(userId2));
        Assert.That(events[1].Name, Is.EqualTo("Bob"));
    }

    [Test]
    public async Task ReadAsync_FromPosition_ReturnsEventsAfterPosition()
    {
        // Arrange
        var stream = "test-users-6";
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var userId3 = Guid.NewGuid();
        
        await _store!.AppendAsync(stream, new UserCreated(userId1, "Alice", "alice@example.com"));
        await _store.AppendAsync(stream, new UserCreated(userId2, "Bob", "bob@example.com"));
        await _store.AppendAsync(stream, new UserCreated(userId3, "Charlie", "charlie@example.com"));

        // Act - Read from position 1
        var events = new List<UserCreated>();
        await foreach (var storedEvent in _store.ReadAsync<UserCreated>(stream, fromPosition: 1))
        {
            events.Add(storedEvent.Event);
        }

        // Assert - Should only get Bob and Charlie
        Assert.That(events, Has.Count.EqualTo(2));
        Assert.That(events[0].UserId, Is.EqualTo(userId2));
        Assert.That(events[1].UserId, Is.EqualTo(userId3));
    }

    [Test]
    public async Task ReadAsync_WithPositions_ReturnsCorrectPositions()
    {
        // Arrange
        var stream = "test-users-7";
        await _store!.AppendAsync(stream, new UserCreated(Guid.NewGuid(), "Alice", "alice@example.com"));
        await _store.AppendAsync(stream, new UserCreated(Guid.NewGuid(), "Bob", "bob@example.com"));

        // Act
        var positions = new List<long>();
        await foreach (var storedEvent in _store.ReadAsync<UserCreated>(stream))
        {
            positions.Add(storedEvent.Position);
        }

        // Assert
        Assert.That(positions, Is.EqualTo(new[] { 0L, 1L }));
    }

    [Test]
    public async Task GetStreamPositionAsync_EmptyStream_ReturnsNull()
    {
        // Arrange
        var stream = "test-users-8";

        // Act
        var position = await _store!.GetStreamPositionAsync(stream);

        // Assert
        Assert.That(position, Is.Null);
    }

    [Test]
    public async Task GetStreamPositionAsync_WithEvents_ReturnsLastPosition()
    {
        // Arrange
        var stream = "test-users-9";
        await _store!.AppendAsync(stream, new UserCreated(Guid.NewGuid(), "Alice", "alice@example.com"));
        await _store.AppendAsync(stream, new UserCreated(Guid.NewGuid(), "Bob", "bob@example.com"));
        await _store.AppendAsync(stream, new UserCreated(Guid.NewGuid(), "Charlie", "charlie@example.com"));

        // Act
        var position = await _store.GetStreamPositionAsync(stream);

        // Assert
        Assert.That(position, Is.EqualTo(2));
    }

    [Test]
    public async Task GetStreamPositionAsync_WithNullStream_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _store!.GetStreamPositionAsync(null!));
    }

    [Test]
    public async Task DifferentStreams_AreIndependent()
    {
        // Arrange
        var stream1 = "test-users-10";
        var stream2 = "test-orders-1";

        // Act
        await _store!.AppendAsync(stream1, new UserCreated(Guid.NewGuid(), "Alice", "alice@example.com"));
        await _store.AppendAsync(stream2, new OrderPlaced(Guid.NewGuid(), 100.50m));
        await _store.AppendAsync(stream1, new UserCreated(Guid.NewGuid(), "Bob", "bob@example.com"));

        // Assert
        var stream1Position = await _store.GetStreamPositionAsync(stream1);
        var stream2Position = await _store.GetStreamPositionAsync(stream2);

        Assert.That(stream1Position, Is.EqualTo(1));
        Assert.That(stream2Position, Is.EqualTo(0));
    }

    [Test]
    public async Task ReadAsync_FiltersByEventType()
    {
        // Arrange
        var stream = "test-mixed-1";
        var userId = Guid.NewGuid();
        
        await _store!.AppendAsync(stream, new UserCreated(userId, "Alice", "alice@example.com"));
        await _store.AppendAsync(stream, new UserUpdated(userId, "Alice Smith"));
        await _store.AppendAsync(stream, new UserCreated(Guid.NewGuid(), "Bob", "bob@example.com"));

        // Act - Read only UserCreated events
        var createdEvents = new List<UserCreated>();
        await foreach (var storedEvent in _store.ReadAsync<UserCreated>(stream))
        {
            createdEvents.Add(storedEvent.Event);
        }

        // Act - Read only UserUpdated events
        var updatedEvents = new List<UserUpdated>();
        await foreach (var storedEvent in _store.ReadAsync<UserUpdated>(stream))
        {
            updatedEvents.Add(storedEvent.Event);
        }

        // Assert
        Assert.That(createdEvents, Has.Count.EqualTo(2));
        Assert.That(updatedEvents, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ParallelAppends_ToSameStream_AreThreadSafe()
    {
        // Arrange
        var stream = "test-users-11";
        var iterations = 10;

        // Act - Append from multiple threads
        var tasks = Enumerable.Range(0, iterations)
            .Select(async i => 
                await _store!.AppendAsync(stream, new UserCreated(Guid.NewGuid(), $"User{i}", $"user{i}@example.com")))
            .ToArray();

        var positions = await Task.WhenAll(tasks);

        // Assert
        var finalPosition = await _store!.GetStreamPositionAsync(stream);
        Assert.That(finalPosition, Is.EqualTo(iterations - 1));
        Assert.That(positions.Distinct().Count(), Is.EqualTo(iterations)); // All positions should be unique
    }

    [Test]
    public async Task ParallelAppends_ToDifferentStreams_AreIsolated()
    {
        // Arrange
        var streamCount = 5;
        var eventsPerStream = 3;

        // Act - Append to multiple streams in parallel
        var tasks = Enumerable.Range(0, streamCount)
            .Select(async streamIndex =>
            {
                var stream = $"test-users-parallel-{streamIndex}";
                for (var i = 0; i < eventsPerStream; i++)
                {
                    await _store!.AppendAsync(stream, 
                        new UserCreated(Guid.NewGuid(), $"User{i}", $"user{i}@example.com"));
                }
            })
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert - Each stream should have the correct number of events
        for (var streamIndex = 0; streamIndex < streamCount; streamIndex++)
        {
            var stream = $"test-users-parallel-{streamIndex}";
            var position = await _store!.GetStreamPositionAsync(stream);
            Assert.That(position, Is.EqualTo(eventsPerStream - 1));
        }
    }
}
