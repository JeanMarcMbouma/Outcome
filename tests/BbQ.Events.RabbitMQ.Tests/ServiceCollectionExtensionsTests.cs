using BbQ.Events.Configuration;
using BbQ.Events.Events;
using BbQ.Events.RabbitMQ.Configuration;
using BbQ.Events.RabbitMQ.Events;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BbQ.Events.RabbitMQ.Tests;

/// <summary>
/// Tests for dependency injection extensions.
/// </summary>
[TestFixture]
public class ServiceCollectionExtensionsTests
{
    [Test]
    public void UseRabbitMqEventBus_WithConnectionUri_RegistersEventBus()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.UseRabbitMqEventBus("amqp://guest:guest@localhost:5672/");
        var provider = services.BuildServiceProvider();

        // Assert
        var bus = provider.GetService<IEventBus>();
        Assert.That(bus, Is.Not.Null);
    }

    [Test]
    public void UseRabbitMqEventBus_WithConnectionUri_RegistersEventPublisher()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.UseRabbitMqEventBus("amqp://guest:guest@localhost:5672/");
        var provider = services.BuildServiceProvider();

        // Assert
        var publisher = provider.GetService<IEventPublisher>();
        Assert.That(publisher, Is.Not.Null);
    }

    [Test]
    public void UseRabbitMqEventBus_WithNullConnectionUri_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            services.UseRabbitMqEventBus((string)null!));
    }

    [Test]
    public void UseRabbitMqEventBus_WithEmptyConnectionUri_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            services.UseRabbitMqEventBus(""));
    }

    [Test]
    public void UseRabbitMqEventBus_WithOptions_RegistersEventBus()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.UseRabbitMqEventBus(options =>
        {
            options.HostName = "localhost";
            options.Port = 5672;
            options.UserName = "guest";
            options.Password = "guest";
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var bus = provider.GetService<IEventBus>();
        Assert.That(bus, Is.Not.Null);
    }

    [Test]
    public void UseRabbitMqEventBus_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            services.UseRabbitMqEventBus((Action<RabbitMqEventBusOptions>)null!));
    }

    [Test]
    public void UseRabbitMqEventBus_ReplacesExistingEventBus()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();

        // Act
        services.UseRabbitMqEventBus("amqp://guest:guest@localhost:5672/");
        var provider = services.BuildServiceProvider();

        // Assert
        var bus = provider.GetService<IEventBus>();
        Assert.That(bus, Is.Not.Null);
        // Verify it's not the in-memory type (RabbitMQ type replaced it)
        Assert.That(bus!.GetType().Name, Does.Not.Contain("InMemory"));
    }

    [Test]
    public void UseRabbitMqEventBus_ReturnsSameServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        var result = services.UseRabbitMqEventBus("amqp://guest:guest@localhost:5672/");

        // Assert
        Assert.That(result, Is.SameAs(services));
    }

    [Test]
    public void UseRabbitMqEventBus_EventPublisherResolvesToSameInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.UseRabbitMqEventBus("amqp://guest:guest@localhost:5672/");
        var provider = services.BuildServiceProvider();

        // Assert
        var bus = provider.GetService<IEventBus>();
        var publisher = provider.GetService<IEventPublisher>();
        Assert.That(publisher, Is.SameAs(bus));
    }

    [Test]
    public void UseRabbitMqEventBus_WithOptions_CustomExchangeName()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.UseRabbitMqEventBus(options =>
        {
            options.HostName = "localhost";
            options.ExchangeName = "custom.exchange";
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var bus = provider.GetService<IEventBus>();
        Assert.That(bus, Is.Not.Null);
    }
}
