using BbQ.Events.SqlServer;
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
}
