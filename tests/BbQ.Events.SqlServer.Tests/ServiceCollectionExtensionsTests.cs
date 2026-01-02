using BbQ.Events.Checkpointing;
using BbQ.Events.Events;
using BbQ.Events.SqlServer.Checkpointing;
using BbQ.Events.SqlServer.Configuration;
using BbQ.Events.SqlServer.Events;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BbQ.Events.SqlServer.Tests;

/// <summary>
/// Tests for dependency injection extensions.
/// </summary>
[TestFixture]
public class ServiceCollectionExtensionsTests
{
    [Test]
    public void UseSqlServerCheckpoints_RegistersCheckpointStore()
    {
        // Arrange
        var services = new ServiceCollection();
        var connectionString = "Server=localhost;Database=Test;";

        // Act
        services.UseSqlServerCheckpoints(connectionString);
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetService<IProjectionCheckpointStore>();
        Assert.That(store, Is.Not.Null);
        Assert.That(store, Is.InstanceOf<SqlServerProjectionCheckpointStore>());
    }

    [Test]
    public void UseSqlServerCheckpoints_WithNullConnectionString_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            services.UseSqlServerCheckpoints(null!));
    }

    [Test]
    public void UseSqlServerCheckpoints_WithEmptyConnectionString_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            services.UseSqlServerCheckpoints(""));
    }

    [Test]
    public void UseSqlServerCheckpoints_ReplacesExistingCheckpointStore()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IProjectionCheckpointStore, InMemoryProjectionCheckpointStore>();
        
        var connectionString = "Server=localhost;Database=Test;";

        // Act
        services.UseSqlServerCheckpoints(connectionString);
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetService<IProjectionCheckpointStore>();
        Assert.That(store, Is.Not.Null);
        Assert.That(store, Is.InstanceOf<SqlServerProjectionCheckpointStore>());
        Assert.That(store, Is.Not.InstanceOf<InMemoryProjectionCheckpointStore>());
    }

    [Test]
    public void UseSqlServerCheckpoints_ReturnsSameServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        var connectionString = "Server=localhost;Database=Test;";

        // Act
        var result = services.UseSqlServerCheckpoints(connectionString);

        // Assert
        Assert.That(result, Is.SameAs(services));
    }

    [Test]
    public void UseSqlServerEventStore_RegistersEventStore()
    {
        // Arrange
        var services = new ServiceCollection();
        var connectionString = "Server=localhost;Database=Test;";

        // Act
        services.UseSqlServerEventStore(connectionString);
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetService<IEventStore>();
        Assert.That(store, Is.Not.Null);
        Assert.That(store, Is.InstanceOf<SqlServerEventStore>());
    }

    [Test]
    public void UseSqlServerEventStore_WithNullConnectionString_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            services.UseSqlServerEventStore((string)null!));
    }

    [Test]
    public void UseSqlServerEventStore_WithEmptyConnectionString_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            services.UseSqlServerEventStore(""));
    }

    [Test]
    public void UseSqlServerEventStore_WithOptions_RegistersEventStore()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.UseSqlServerEventStore(options =>
        {
            options.ConnectionString = "Server=localhost;Database=Test;";
            options.IncludeMetadata = true;
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetService<IEventStore>();
        Assert.That(store, Is.Not.Null);
        Assert.That(store, Is.InstanceOf<SqlServerEventStore>());
    }

    [Test]
    public void UseSqlServerEventStore_WithOptionsButNoConnectionString_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            services.UseSqlServerEventStore(options => { }));
    }

    [Test]
    public void UseSqlServerEventStore_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            services.UseSqlServerEventStore((Action<SqlServerEventStoreOptions>)null!));
    }

    [Test]
    public void UseSqlServerEventStore_ReplacesExistingEventStore()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore, InMemoryEventStore>();
        
        var connectionString = "Server=localhost;Database=Test;";

        // Act
        services.UseSqlServerEventStore(connectionString);
        var provider = services.BuildServiceProvider();

        // Assert
        var store = provider.GetService<IEventStore>();
        Assert.That(store, Is.Not.Null);
        Assert.That(store, Is.InstanceOf<SqlServerEventStore>());
        Assert.That(store, Is.Not.InstanceOf<InMemoryEventStore>());
    }

    [Test]
    public void UseSqlServerEventStore_ReturnsSameServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        var connectionString = "Server=localhost;Database=Test;";

        // Act
        var result = services.UseSqlServerEventStore(connectionString);

        // Assert
        Assert.That(result, Is.SameAs(services));
    }
}
