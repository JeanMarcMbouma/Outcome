using BbQ.Events.PostgreSql.Checkpointing;
using Npgsql;
using NUnit.Framework;
using Testcontainers.PostgreSql;

namespace BbQ.Events.PostgreSql.Tests;

/// <summary>
/// Integration tests for PostgreSqlProjectionCheckpointStore.
/// 
/// These tests use Testcontainers to spin up a PostgreSQL instance.
/// Docker must be running for these tests to execute.
/// </summary>
[TestFixture]
public class PostgreSqlProjectionCheckpointStoreTests
{
    private PostgreSqlContainer? _postgresContainer;
    private string? _connectionString;
    private PostgreSqlProjectionCheckpointStore? _store;
    private bool _canRunTests;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        try
        {
            // Start PostgreSQL container
            _postgresContainer = new PostgreSqlBuilder()
                .WithImage("postgres:16")
                .WithDatabase("bbqeventstest")
                .WithUsername("testuser")
                .WithPassword("testpassword")
                .Build();

            await _postgresContainer.StartAsync();

            _connectionString = _postgresContainer.GetConnectionString();

            // Create the checkpoint table
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS bbq_projection_checkpoints (
                    projection_name TEXT NOT NULL,
                    partition_key TEXT NULL DEFAULT NULL,
                    position BIGINT NOT NULL,
                    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                    CONSTRAINT pk_bbq_projection_checkpoints PRIMARY KEY (projection_name, partition_key) NULLS NOT DISTINCT
                );";
            
            await command.ExecuteNonQueryAsync();

            _canRunTests = true;
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"PostgreSQL container could not be started: {ex.Message}");
            TestContext.WriteLine("Tests will be skipped. Ensure Docker is running.");
            _canRunTests = false;
        }
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    [SetUp]
    public async Task SetUp()
    {
        if (!_canRunTests)
        {
            Assert.Ignore("PostgreSQL container not available for testing");
            return;
        }

        _store = new PostgreSqlProjectionCheckpointStore(_connectionString!);

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

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM bbq_projection_checkpoints WHERE projection_name LIKE 'Test%'";
        await command.ExecuteNonQueryAsync();
    }

    [Test]
    public void Constructor_WithNullConnectionString_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new PostgreSqlProjectionCheckpointStore(null!));
        Assert.Throws<ArgumentNullException>(() => new PostgreSqlProjectionCheckpointStore(""));
        Assert.Throws<ArgumentNullException>(() => new PostgreSqlProjectionCheckpointStore("   "));
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
