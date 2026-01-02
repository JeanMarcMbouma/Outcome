using BbQ.Events.SqlServer.Checkpointing;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace BbQ.Events.SqlServer.Tests;

/// <summary>
/// Integration tests for SqlServerProjectionCheckpointStore.
/// 
/// These tests require a SQL Server instance (LocalDB or full SQL Server).
/// Set the connection string via environment variable: TEST_SQLSERVER_CONNECTION_STRING
/// 
/// If no connection string is provided, tests will be skipped.
/// </summary>
[TestFixture]
public class SqlServerProjectionCheckpointStoreTests
{
    private string? _connectionString;
    private SqlServerProjectionCheckpointStore? _store;
    private bool _canRunTests;

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

            // Now connect to the test database to create the table
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Create table
            var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BbQ_ProjectionCheckpoints')
                BEGIN
                    CREATE TABLE BbQ_ProjectionCheckpoints (
                        ProjectionName NVARCHAR(200) NOT NULL,
                        PartitionKey NVARCHAR(200) NULL,
                        Position BIGINT NOT NULL,
                        LastUpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                        PRIMARY KEY (ProjectionName, PartitionKey)
                    );
                END";
            
            await createTableCommand.ExecuteNonQueryAsync();

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

        _store = new SqlServerProjectionCheckpointStore(_connectionString!);

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
        // Be defensive: if tests cannot run or the connection string is not available,
        // there is nothing to clean up.
        if (!_canRunTests || string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM BbQ_ProjectionCheckpoints WHERE ProjectionName LIKE 'Test%'";
        await command.ExecuteNonQueryAsync();
    }

    [Test]
    public void Constructor_WithNullConnectionString_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SqlServerProjectionCheckpointStore(null!));
        Assert.Throws<ArgumentNullException>(() => new SqlServerProjectionCheckpointStore(""));
        Assert.Throws<ArgumentNullException>(() => new SqlServerProjectionCheckpointStore("   "));
    }

    [Test]
    public async Task GetCheckpointAsync_WithNoCheckpoint_ReturnsNull()
    {
        // Arrange
        var projectionName = "TestProjection_NoCheckpoint";

        // Act
        var result = await _store!.GetCheckpointAsync(projectionName);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task SaveCheckpointAsync_WithNewCheckpoint_CreatesCheckpoint()
    {
        // Arrange
        var projectionName = "TestProjection_NewCheckpoint";
        var checkpoint = 100L;

        // Act
        await _store!.SaveCheckpointAsync(projectionName, checkpoint);

        // Assert
        var result = await _store.GetCheckpointAsync(projectionName);
        Assert.That(result, Is.EqualTo(checkpoint));
    }

    [Test]
    public async Task SaveCheckpointAsync_WithExistingCheckpoint_UpdatesCheckpoint()
    {
        // Arrange
        var projectionName = "TestProjection_UpdateCheckpoint";
        await _store!.SaveCheckpointAsync(projectionName, 100);

        // Act
        await _store.SaveCheckpointAsync(projectionName, 200);

        // Assert
        var result = await _store.GetCheckpointAsync(projectionName);
        Assert.That(result, Is.EqualTo(200));
    }

    [Test]
    public async Task SaveCheckpointAsync_Multiple_UpdatesCheckpoint()
    {
        // Arrange
        var projectionName = "TestProjection_MultipleUpdates";

        // Act & Assert
        await _store!.SaveCheckpointAsync(projectionName, 100);
        Assert.That(await _store.GetCheckpointAsync(projectionName), Is.EqualTo(100));

        await _store.SaveCheckpointAsync(projectionName, 200);
        Assert.That(await _store.GetCheckpointAsync(projectionName), Is.EqualTo(200));

        await _store.SaveCheckpointAsync(projectionName, 300);
        Assert.That(await _store.GetCheckpointAsync(projectionName), Is.EqualTo(300));
    }

    [Test]
    public async Task ResetCheckpointAsync_WithExistingCheckpoint_RemovesCheckpoint()
    {
        // Arrange
        var projectionName = "TestProjection_ResetCheckpoint";
        await _store!.SaveCheckpointAsync(projectionName, 100);

        // Act
        await _store.ResetCheckpointAsync(projectionName);

        // Assert
        var result = await _store.GetCheckpointAsync(projectionName);
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ResetCheckpointAsync_WithNoCheckpoint_DoesNotThrow()
    {
        // Arrange
        var projectionName = "TestProjection_ResetNonExistent";

        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await _store!.ResetCheckpointAsync(projectionName));
    }

    [Test]
    public async Task ParallelWrites_ToSameProjection_AreThreadSafe()
    {
        // Arrange
        var projectionName = "TestProjection_ParallelWrites";
        var iterations = 50;

        // Act - Write from multiple threads
        var tasks = Enumerable.Range(0, iterations)
            .Select(async i => await _store!.SaveCheckpointAsync(projectionName, i))
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert - Should have a valid checkpoint (last write wins)
        var result = await _store!.GetCheckpointAsync(projectionName);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.GreaterThanOrEqualTo(0));
        Assert.That(result, Is.LessThan(iterations));
    }

    [Test]
    public async Task ParallelWrites_ToDifferentProjections_AreIsolated()
    {
        // Arrange
        var projectionCount = 10;
        var checkpoint = 100L;

        // Act - Write to multiple projections in parallel
        var tasks = Enumerable.Range(0, projectionCount)
            .Select(async i =>
            {
                var projectionName = $"TestProjection_Parallel_{i}";
                await _store!.SaveCheckpointAsync(projectionName, checkpoint + i);
            })
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert - Each projection should have its own checkpoint
        for (var i = 0; i < projectionCount; i++)
        {
            var projectionName = $"TestProjection_Parallel_{i}";
            var result = await _store!.GetCheckpointAsync(projectionName);
            Assert.That(result, Is.EqualTo(checkpoint + i));
        }
    }

    [Test]
    public async Task GetCheckpointAsync_WithNullProjectionName_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(async () => 
            await _store!.GetCheckpointAsync(null!));
    }

    [Test]
    public async Task SaveCheckpointAsync_WithNullProjectionName_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(async () => 
            await _store!.SaveCheckpointAsync(null!, 100));
    }

    [Test]
    public async Task ResetCheckpointAsync_WithNullProjectionName_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(async () => 
            await _store!.ResetCheckpointAsync(null!));
    }

    [Test]
    public async Task Checkpoint_WithDifferentProjectionNames_AreIndependent()
    {
        // Arrange
        var projection1 = "TestProjection_Independent_1";
        var projection2 = "TestProjection_Independent_2";

        // Act
        await _store!.SaveCheckpointAsync(projection1, 100);
        await _store.SaveCheckpointAsync(projection2, 200);

        // Assert
        Assert.That(await _store.GetCheckpointAsync(projection1), Is.EqualTo(100));
        Assert.That(await _store.GetCheckpointAsync(projection2), Is.EqualTo(200));

        // Reset one projection
        await _store.ResetCheckpointAsync(projection1);

        // Assert - Only projection1 is reset
        Assert.That(await _store.GetCheckpointAsync(projection1), Is.Null);
        Assert.That(await _store.GetCheckpointAsync(projection2), Is.EqualTo(200));
    }

    [Test]
    public async Task SaveCheckpointAsync_WithLargeCheckpointValue_WorksCorrectly()
    {
        // Arrange
        var projectionName = "TestProjection_LargeValue";
        var largeCheckpoint = long.MaxValue - 1000;

        // Act
        await _store!.SaveCheckpointAsync(projectionName, largeCheckpoint);

        // Assert
        var result = await _store.GetCheckpointAsync(projectionName);
        Assert.That(result, Is.EqualTo(largeCheckpoint));
    }

    [Test]
    public async Task SaveCheckpointAsync_WithZeroCheckpoint_WorksCorrectly()
    {
        // Arrange
        var projectionName = "TestProjection_ZeroValue";

        // Act
        await _store!.SaveCheckpointAsync(projectionName, 0);

        // Assert
        var result = await _store.GetCheckpointAsync(projectionName);
        Assert.That(result, Is.EqualTo(0));
    }
}
