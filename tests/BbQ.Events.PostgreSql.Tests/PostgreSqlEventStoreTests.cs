using BbQ.Events.PostgreSql.Events;
using Npgsql;
using NUnit.Framework;

namespace BbQ.Events.PostgreSql.Tests;

/// <summary>
/// Integration tests for PostgreSqlEventStore.
/// 
/// These tests require a PostgreSQL instance.
/// Set the connection string via environment variable: TEST_POSTGRESQL_CONNECTION_STRING
/// 
/// If no connection string is provided, tests will be skipped.
/// </summary>
[TestFixture]
public class PostgreSqlEventStoreTests
{
    private string? _connectionString;
    private PostgreSqlEventStore? _store;
    private bool _canRunTests;

    // Test event types
    private record UserCreated(Guid UserId, string Name, string Email);
    private record UserUpdated(Guid UserId, string Name);
    private record OrderPlaced(Guid OrderId, decimal Amount);

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Try to get connection string from environment variable
        _connectionString = Environment.GetEnvironmentVariable("TEST_POSTGRESQL_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            // Try default local PostgreSQL as fallback (without password - relies on trust authentication or .pgpass)
            _connectionString = "Host=localhost;Database=bbqeventstest;Username=postgres";
        }

        try
        {
            // First, connect to postgres database to create the test database if needed
            var builder = new NpgsqlConnectionStringBuilder(_connectionString);
            var testDatabase = builder.Database;
            builder.Database = "postgres";
            var postgresConnectionString = builder.ToString();
            
            await using (var postgresConnection = new NpgsqlConnection(postgresConnectionString))
            {
                await postgresConnection.OpenAsync();

                // Create test database if it doesn't exist
                var createDbCommand = postgresConnection.CreateCommand();
                createDbCommand.CommandText = @"SELECT 1 FROM pg_database WHERE datname = @testDatabase";
                createDbCommand.Parameters.AddWithValue("@testDatabase", testDatabase ?? "bbqeventstest");
                
                var exists = await createDbCommand.ExecuteScalarAsync();
                
                if (exists == null && !string.IsNullOrEmpty(testDatabase))
                {
                    // PostgreSQL doesn't support parameters for CREATE DATABASE, so we need to validate the name
                    // Database names can only contain alphanumeric characters and underscores
                    if (!System.Text.RegularExpressions.Regex.IsMatch(testDatabase, @"^[a-zA-Z0-9_]+$"))
                    {
                        throw new InvalidOperationException($"Invalid database name: {testDatabase}");
                    }
                    
                    createDbCommand.CommandText = $@"CREATE DATABASE {testDatabase}";
                    createDbCommand.Parameters.Clear();
                    
                    try
                    {
                        await createDbCommand.ExecuteNonQueryAsync();
                    }
                    catch (Exception ex)
                    {
                        // Log the error so issues like missing permissions are visible in test output
                        TestContext.WriteLine($"Warning: Failed to create test database '{testDatabase}'. Exception: {ex.Message}");
                    }
                }
            }

            // Now connect to the test database to create the tables
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Create Streams table
            var createStreamsTableCommand = connection.CreateCommand();
            createStreamsTableCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS bbq_streams (
                    stream_name VARCHAR(200) PRIMARY KEY,
                    current_position BIGINT NOT NULL DEFAULT -1,
                    version INT NOT NULL DEFAULT 0,
                    created_utc TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
                    last_updated_utc TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')
                )";
            
            await createStreamsTableCommand.ExecuteNonQueryAsync();

            // Create Events table
            var createEventsTableCommand = connection.CreateCommand();
            createEventsTableCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS bbq_events (
                    event_id BIGSERIAL PRIMARY KEY,
                    stream_name VARCHAR(200) NOT NULL,
                    position BIGINT NOT NULL,
                    event_type VARCHAR(500) NOT NULL,
                    event_data TEXT NOT NULL,
                    metadata TEXT NULL,
                    created_utc TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
                    CONSTRAINT uq_bbq_events_stream_position UNIQUE (stream_name, position)
                );
                
                CREATE INDEX IF NOT EXISTS ix_bbq_events_stream_name_position 
                ON bbq_events(stream_name, position)";
            
            await createEventsTableCommand.ExecuteNonQueryAsync();

            _canRunTests = true;
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"PostgreSQL not available: {ex.Message}");
            TestContext.WriteLine("Tests will be skipped. To run tests, ensure PostgreSQL is available.");
            _canRunTests = false;
        }
    }

    [SetUp]
    public async Task SetUp()
    {
        if (!_canRunTests)
        {
            Assert.Ignore("PostgreSQL not available for testing");
            return;
        }

        var options = new PostgreSqlEventStoreOptions
        {
            ConnectionString = _connectionString!
        };
        _store = new PostgreSqlEventStore(options);

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

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // Delete test events
        await using var deleteEventsCommand = connection.CreateCommand();
        deleteEventsCommand.CommandText = "DELETE FROM bbq_events WHERE stream_name LIKE 'test-%'";
        await deleteEventsCommand.ExecuteNonQueryAsync();

        // Delete test streams
        await using var deleteStreamsCommand = connection.CreateCommand();
        deleteStreamsCommand.CommandText = "DELETE FROM bbq_streams WHERE stream_name LIKE 'test-%'";
        await deleteStreamsCommand.ExecuteNonQueryAsync();
    }

    [Test]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new PostgreSqlEventStore(null!));
    }

    [Test]
    public void Constructor_WithEmptyConnectionString_ThrowsArgumentException()
    {
        // Arrange
        var options = new PostgreSqlEventStoreOptions { ConnectionString = "" };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new PostgreSqlEventStore(options));
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

    [Test]
    public async Task IncludeMetadata_WhenEnabled_StoresMetadataWithEvents()
    {
        // Arrange
        var stream = "test-metadata-1";
        var options = new PostgreSqlEventStoreOptions
        {
            ConnectionString = _connectionString!,
            IncludeMetadata = true
        };
        var storeWithMetadata = new PostgreSqlEventStore(options);

        // Act
        var userId = Guid.NewGuid();
        await storeWithMetadata.AppendAsync(stream, new UserCreated(userId, "Alice", "alice@example.com"));

        // Assert - Check metadata was stored in database
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT metadata FROM bbq_events WHERE stream_name = @stream_name";
        command.Parameters.AddWithValue("@stream_name", stream);

        var metadata = await command.ExecuteScalarAsync();
        Assert.That(metadata, Is.Not.Null);
        Assert.That(metadata, Is.Not.EqualTo(DBNull.Value));

        var metadataString = metadata.ToString();
        Assert.That(metadataString, Does.Contain("timestamp"));
        Assert.That(metadataString, Does.Contain("server"));
    }

    [Test]
    public async Task IncludeMetadata_WhenDisabled_StoresNullMetadata()
    {
        // Arrange
        var stream = "test-metadata-2";
        // Default options have IncludeMetadata = false

        // Act
        var userId = Guid.NewGuid();
        await _store!.AppendAsync(stream, new UserCreated(userId, "Bob", "bob@example.com"));

        // Assert - Check metadata is null in database
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT metadata FROM bbq_events WHERE stream_name = @stream_name";
        command.Parameters.AddWithValue("@stream_name", stream);

        var metadata = await command.ExecuteScalarAsync();
        Assert.That(metadata, Is.EqualTo(DBNull.Value));
    }

    [Test]
    public async Task CustomJsonOptions_WithPascalCase_SerializesCorrectly()
    {
        // Arrange
        var stream = "test-json-1";
        var customOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = null // PascalCase (default)
        };

        var options = new PostgreSqlEventStoreOptions
        {
            ConnectionString = _connectionString!,
            JsonSerializerOptions = customOptions
        };
        var storeWithCustomJson = new PostgreSqlEventStore(options);

        // Act
        var userId = Guid.NewGuid();
        await storeWithCustomJson.AppendAsync(stream, new UserCreated(userId, "Charlie", "charlie@example.com"));

        // Assert - Check JSON uses PascalCase
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT event_data FROM bbq_events WHERE stream_name = @stream_name";
        command.Parameters.AddWithValue("@stream_name", stream);

        var eventData = (await command.ExecuteScalarAsync())?.ToString();
        Assert.That(eventData, Is.Not.Null);
        Assert.That(eventData, Does.Contain("UserId")); // PascalCase
        Assert.That(eventData, Does.Contain("Name"));
        Assert.That(eventData, Does.Not.Contain("userId")); // Should not be camelCase
    }

    [Test]
    public async Task CustomJsonOptions_RoundTrip_DeserializesCorrectly()
    {
        // Arrange
        var stream = "test-json-2";
        var customOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
        };

        var options = new PostgreSqlEventStoreOptions
        {
            ConnectionString = _connectionString!,
            JsonSerializerOptions = customOptions
        };
        var storeWithCustomJson = new PostgreSqlEventStore(options);

        // Act
        var userId = Guid.NewGuid();
        var originalEvent = new UserCreated(userId, "David", "david@example.com");
        await storeWithCustomJson.AppendAsync(stream, originalEvent);

        // Read back using the same store (with same JSON options)
        var events = new List<UserCreated>();
        await foreach (var storedEvent in storeWithCustomJson.ReadAsync<UserCreated>(stream))
        {
            events.Add(storedEvent.Event);
        }

        // Assert
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].UserId, Is.EqualTo(originalEvent.UserId));
        Assert.That(events[0].Name, Is.EqualTo(originalEvent.Name));
        Assert.That(events[0].Email, Is.EqualTo(originalEvent.Email));
    }
}
