using BbQ.Events.Checkpointing;
using BbQ.Events.Events;
using BbQ.Events.PostgreSql.Configuration;
using BbQ.Events.PostgreSql.Checkpointing;
using BbQ.Events.PostgreSql.Events;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BbQ.Events.PostgreSql.Tests;

/// <summary>
/// Tests for PostgreSQL service collection extensions.
/// </summary>
[TestFixture]
public class ServiceCollectionExtensionsTests
{
    [Test]
    public void UsePostgreSqlCheckpoints_RegistersCheckpointStore()
    {
        // Arrange
        var services = new ServiceCollection();
        var connectionString = "Host=localhost;Database=test;Username=test;Password=test";

        // Act
        services.UsePostgreSqlCheckpoints(connectionString);
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetService<IProjectionCheckpointStore>();
        Assert.That(store, Is.Not.Null);
        Assert.That(store, Is.InstanceOf<PostgreSqlProjectionCheckpointStore>());
    }

    [Test]
    public void UsePostgreSqlCheckpoints_WithNullConnectionString_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => services.UsePostgreSqlCheckpoints(null!));
        Assert.Throws<ArgumentNullException>(() => services.UsePostgreSqlCheckpoints(""));
        Assert.Throws<ArgumentNullException>(() => services.UsePostgreSqlCheckpoints("   "));
    }

    [Test]
    public void UsePostgreSqlCheckpoints_ReplacesExistingCheckpointStore()
    {
        // Arrange
        var services = new ServiceCollection();
        var connectionString1 = "Host=localhost;Database=test1;Username=test;Password=test";
        var connectionString2 = "Host=localhost;Database=test2;Username=test;Password=test";

        // Act - Register twice with different connection strings
        services.UsePostgreSqlCheckpoints(connectionString1);
        services.UsePostgreSqlCheckpoints(connectionString2);

        // Assert - Should only have one registration
        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IProjectionCheckpointStore>();
        
        Assert.That(store, Is.Not.Null);
        Assert.That(store, Is.InstanceOf<PostgreSqlProjectionCheckpointStore>());
        
        // Verify there's only one registration (the second one replaced the first)
        var descriptors = services.Where(d => d.ServiceType == typeof(IProjectionCheckpointStore)).ToList();
        Assert.That(descriptors, Has.Count.EqualTo(1));
    }

    [Test]
    public void UsePostgreSqlCheckpoints_ReturnsServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        var connectionString = "Host=localhost;Database=test;Username=test;Password=test";

        // Act
        var result = services.UsePostgreSqlCheckpoints(connectionString);

        // Assert
        Assert.That(result, Is.SameAs(services));
    }

    [Test]
    public void UsePostgreSqlEventStore_WithConnectionString_RegistersEventStore()
    {
        // Arrange
        var services = new ServiceCollection();
        var connectionString = "Host=localhost;Database=test;Username=test;Password=test";

        // Act
        services.UsePostgreSqlEventStore(connectionString);
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetService<IEventStore>();
        Assert.That(store, Is.Not.Null);
        Assert.That(store, Is.InstanceOf<PostgreSqlEventStore>());
    }

    [Test]
    public void UsePostgreSqlEventStore_WithOptions_RegistersEventStore()
    {
        // Arrange
        var services = new ServiceCollection();
        var connectionString = "Host=localhost;Database=test;Username=test;Password=test";

        // Act
        services.UsePostgreSqlEventStore(options =>
        {
            options.ConnectionString = connectionString;
            options.IncludeMetadata = true;
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetService<IEventStore>();
        Assert.That(store, Is.Not.Null);
        Assert.That(store, Is.InstanceOf<PostgreSqlEventStore>());
    }

    [Test]
    public void UsePostgreSqlEventStore_WithNullConnectionString_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => services.UsePostgreSqlEventStore((string)null!));
        Assert.Throws<ArgumentNullException>(() => services.UsePostgreSqlEventStore(""));
        Assert.Throws<ArgumentNullException>(() => services.UsePostgreSqlEventStore("   "));
    }

    [Test]
    public void UsePostgreSqlEventStore_WithNullConfigureAction_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            services.UsePostgreSqlEventStore((Action<PostgreSqlEventStoreOptions>)null!));
    }

    [Test]
    public void UsePostgreSqlEventStore_WithEmptyConnectionStringInOptions_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            services.UsePostgreSqlEventStore(options =>
            {
                options.ConnectionString = "";
            }));
    }

    [Test]
    public void UsePostgreSqlEventStore_ReplacesExistingEventStore()
    {
        // Arrange
        var services = new ServiceCollection();
        var connectionString1 = "Host=localhost;Database=test1;Username=test;Password=test";
        var connectionString2 = "Host=localhost;Database=test2;Username=test;Password=test";

        // Act - Register twice with different connection strings
        services.UsePostgreSqlEventStore(connectionString1);
        services.UsePostgreSqlEventStore(connectionString2);

        // Assert - Should only have one registration
        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IEventStore>();
        
        Assert.That(store, Is.Not.Null);
        Assert.That(store, Is.InstanceOf<PostgreSqlEventStore>());
        
        // Verify there's only one registration (the second one replaced the first)
        var descriptors = services.Where(d => d.ServiceType == typeof(IEventStore)).ToList();
        Assert.That(descriptors, Has.Count.EqualTo(1));
    }

    [Test]
    public void UsePostgreSqlEventStore_ReturnsServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        var connectionString = "Host=localhost;Database=test;Username=test;Password=test";

        // Act
        var result = services.UsePostgreSqlEventStore(connectionString);

        // Assert
        Assert.That(result, Is.SameAs(services));
    }
}
