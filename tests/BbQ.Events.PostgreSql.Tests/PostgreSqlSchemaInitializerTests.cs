using BbQ.Events.Events;
using BbQ.Events.Schema;
using BbQ.Events.PostgreSql.Configuration;
using BbQ.Events.PostgreSql.Events;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BbQ.Events.PostgreSql.Tests;

/// <summary>
/// Tests for PostgreSQL schema initialization.
/// </summary>
[TestFixture]
public class PostgreSqlSchemaInitializerTests
{
    [Test]
    public void UsePostgreSqlEventStore_WithAutoCreateSchemaEnabled_RegistersSchemaInitializer()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.UsePostgreSqlEventStore(options =>
        {
            options.ConnectionString = "Host=localhost;Database=test;Username=test;Password=test";
            options.AutoCreateSchema = true;
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var initializer = provider.GetService<ISchemaInitializer>();
        Assert.That(initializer, Is.Not.Null);
    }

    [Test]
    public void UsePostgreSqlEventStore_WithAutoCreateSchemaDisabled_RegistersSchemaInitializer()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.UsePostgreSqlEventStore(options =>
        {
            options.ConnectionString = "Host=localhost;Database=test;Username=test;Password=test";
            options.AutoCreateSchema = false;
        });
        var provider = services.BuildServiceProvider();

        // Assert - Initializer is still registered even when AutoCreateSchema is false
        var initializer = provider.GetService<ISchemaInitializer>();
        Assert.That(initializer, Is.Not.Null);
    }

    [Test]
    public void UsePostgreSqlEventStore_DefaultAutoCreateSchemaIsFalse()
    {
        // Arrange
        var options = new PostgreSqlEventStoreOptions
        {
            ConnectionString = "Host=localhost;Database=test;Username=test;Password=test"
        };

        // Assert
        Assert.That(options.AutoCreateSchema, Is.False);
    }

    [Test]
    public void EnsureSchemaAsync_WithNonPostgreSqlEventStore_ThrowsInvalidOperationException()
    {
        // Arrange
        var eventStore = new InMemoryEventStore();

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await eventStore.EnsureSchemaAsync());

        Assert.That(ex!.Message, Does.Contain("PostgreSqlEventStore"));
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
    public void PostgreSqlSchemaInitializer_WithNullConnectionString_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new Schema.PostgreSqlSchemaInitializer(null!));
    }
}
