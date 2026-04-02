using BbQ.Events.RabbitMQ.Events;
using NUnit.Framework;

namespace BbQ.Events.RabbitMQ.Tests;

/// <summary>
/// Tests for RabbitMqEventBusOptions configuration.
/// </summary>
[TestFixture]
public class RabbitMqEventBusOptionsTests
{
    [Test]
    public void DefaultOptions_HasExpectedDefaults()
    {
        // Arrange & Act
        var options = new RabbitMqEventBusOptions();

        // Assert
        Assert.That(options.ConnectionUri, Is.EqualTo(string.Empty));
        Assert.That(options.HostName, Is.EqualTo("localhost"));
        Assert.That(options.Port, Is.EqualTo(5672));
        Assert.That(options.UserName, Is.EqualTo("guest"));
        Assert.That(options.Password, Is.EqualTo("guest"));
        Assert.That(options.VirtualHost, Is.EqualTo("/"));
        Assert.That(options.ExchangeName, Is.EqualTo("bbq.events"));
        Assert.That(options.QueuePrefix, Is.EqualTo("bbq"));
        Assert.That(options.DurableQueues, Is.True);
        Assert.That(options.PersistentMessages, Is.True);
        Assert.That(options.AutoDeleteQueues, Is.False);
        Assert.That(options.JsonSerializerOptions, Is.Null);
    }

    [Test]
    public void Options_CanSetConnectionUri()
    {
        // Arrange
        var options = new RabbitMqEventBusOptions();

        // Act
        options.ConnectionUri = "amqp://user:pass@host:5672/vhost";

        // Assert
        Assert.That(options.ConnectionUri, Is.EqualTo("amqp://user:pass@host:5672/vhost"));
    }

    [Test]
    public void Options_CanSetHostName()
    {
        // Arrange
        var options = new RabbitMqEventBusOptions();

        // Act
        options.HostName = "rabbitmq-server";

        // Assert
        Assert.That(options.HostName, Is.EqualTo("rabbitmq-server"));
    }

    [Test]
    public void Options_CanSetPort()
    {
        // Arrange
        var options = new RabbitMqEventBusOptions();

        // Act
        options.Port = 5673;

        // Assert
        Assert.That(options.Port, Is.EqualTo(5673));
    }

    [Test]
    public void Options_CanSetCredentials()
    {
        // Arrange
        var options = new RabbitMqEventBusOptions();

        // Act
        options.UserName = "myuser";
        options.Password = "mypassword";

        // Assert
        Assert.That(options.UserName, Is.EqualTo("myuser"));
        Assert.That(options.Password, Is.EqualTo("mypassword"));
    }

    [Test]
    public void Options_CanSetVirtualHost()
    {
        // Arrange
        var options = new RabbitMqEventBusOptions();

        // Act
        options.VirtualHost = "/myapp";

        // Assert
        Assert.That(options.VirtualHost, Is.EqualTo("/myapp"));
    }

    [Test]
    public void Options_CanSetExchangeName()
    {
        // Arrange
        var options = new RabbitMqEventBusOptions();

        // Act
        options.ExchangeName = "my-app.events";

        // Assert
        Assert.That(options.ExchangeName, Is.EqualTo("my-app.events"));
    }

    [Test]
    public void Options_CanSetQueuePrefix()
    {
        // Arrange
        var options = new RabbitMqEventBusOptions();

        // Act
        options.QueuePrefix = "my-app";

        // Assert
        Assert.That(options.QueuePrefix, Is.EqualTo("my-app"));
    }

    [Test]
    public void Options_CanSetDurableQueues()
    {
        // Arrange
        var options = new RabbitMqEventBusOptions();

        // Act
        options.DurableQueues = false;

        // Assert
        Assert.That(options.DurableQueues, Is.False);
    }

    [Test]
    public void Options_CanSetPersistentMessages()
    {
        // Arrange
        var options = new RabbitMqEventBusOptions();

        // Act
        options.PersistentMessages = false;

        // Assert
        Assert.That(options.PersistentMessages, Is.False);
    }

    [Test]
    public void Options_CanSetAutoDeleteQueues()
    {
        // Arrange
        var options = new RabbitMqEventBusOptions();

        // Act
        options.AutoDeleteQueues = true;

        // Assert
        Assert.That(options.AutoDeleteQueues, Is.True);
    }

    [Test]
    public void Options_CanSetJsonSerializerOptions()
    {
        // Arrange
        var options = new RabbitMqEventBusOptions();
        var jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };

        // Act
        options.JsonSerializerOptions = jsonOptions;

        // Assert
        Assert.That(options.JsonSerializerOptions, Is.SameAs(jsonOptions));
    }
}
