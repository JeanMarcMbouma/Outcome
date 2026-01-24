using BbQ.Events.Events;
using BbQ.Events.Schema;
using BbQ.Events.SqlServer.Configuration;
using BbQ.Events.SqlServer.Events;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BbQ.Events.SqlServer.Tests;

/// <summary>
/// Tests for SQL Server schema initialization.
/// </summary>
[TestFixture]
public class SqlServerSchemaInitializerTests
{
    [Test]
    public void UseSqlServerEventStore_WithAutoCreateSchemaEnabled_RegistersSchemaInitializer()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.UseSqlServerEventStore(options =>
        {
            options.ConnectionString = "Server=localhost;Database=Test;";
            options.AutoCreateSchema = true;
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var initializer = provider.GetService<ISchemaInitializer>();
        Assert.That(initializer, Is.Not.Null);
    }

    [Test]
    public void UseSqlServerEventStore_WithAutoCreateSchemaDisabled_RegistersSchemaInitializer()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.UseSqlServerEventStore(options =>
        {
            options.ConnectionString = "Server=localhost;Database=Test;";
            options.AutoCreateSchema = false;
        });
        var provider = services.BuildServiceProvider();

        // Assert - Initializer is still registered even when AutoCreateSchema is false
        var initializer = provider.GetService<ISchemaInitializer>();
        Assert.That(initializer, Is.Not.Null);
    }

    [Test]
    public void UseSqlServerEventStore_DefaultAutoCreateSchemaIsFalse()
    {
        // Arrange
        var options = new SqlServerEventStoreOptions
        {
            ConnectionString = "Server=localhost;Database=Test;"
        };

        // Assert
        Assert.That(options.AutoCreateSchema, Is.False);
    }

    [Test]
    public void EnsureSchemaAsync_WithNonSqlServerEventStore_ThrowsInvalidOperationException()
    {
        // Arrange
        var eventStore = new InMemoryEventStore();

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await eventStore.EnsureSchemaAsync());

        Assert.That(ex!.Message, Does.Contain("SqlServerEventStore"));
    }

    [Test]
    public void EnsureSchemaAsync_WithNullEventStore_ThrowsArgumentNullException()
    {
        // Arrange
        IEventStore? eventStore = null;

        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await eventStore!.EnsureSchemaAsync());
    }

    [Test]
    public void SqlServerSchemaInitializer_WithNullConnectionString_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new Schema.SqlServerSchemaInitializer(null!));
    }
}
