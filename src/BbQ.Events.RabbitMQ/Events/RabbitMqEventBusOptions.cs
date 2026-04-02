using System.Text.Json;

namespace BbQ.Events.RabbitMQ.Events;

/// <summary>
/// Configuration options for the RabbitMQ event bus.
/// </summary>
public sealed class RabbitMqEventBusOptions
{
    /// <summary>
    /// Gets or sets the RabbitMQ connection URI.
    /// </summary>
    /// <remarks>
    /// Example: "amqp://guest:guest@localhost:5672/"
    /// </remarks>
    public string ConnectionUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the RabbitMQ hostname.
    /// </summary>
    /// <remarks>
    /// Used when <see cref="ConnectionUri"/> is not provided. Default is "localhost".
    /// </remarks>
    public string HostName { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the RabbitMQ port.
    /// </summary>
    /// <remarks>
    /// Default is 5672 (the standard AMQP port).
    /// </remarks>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Gets or sets the RabbitMQ username.
    /// </summary>
    /// <remarks>
    /// Default is "guest".
    /// </remarks>
    public string UserName { get; set; } = "guest";

    /// <summary>
    /// Gets or sets the RabbitMQ password.
    /// </summary>
    /// <remarks>
    /// Default is "guest".
    /// </remarks>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// Gets or sets the RabbitMQ virtual host.
    /// </summary>
    /// <remarks>
    /// Default is "/".
    /// </remarks>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Gets or sets the exchange name used for event publishing.
    /// </summary>
    /// <remarks>
    /// The exchange is declared as a fanout exchange, ensuring all bound queues
    /// receive published events. Default is "bbq.events".
    /// </remarks>
    public string ExchangeName { get; set; } = "bbq.events";

    /// <summary>
    /// Gets or sets the queue name prefix for subscriber queues.
    /// </summary>
    /// <remarks>
    /// Each subscriber creates a queue named "{QueuePrefix}.{EventTypeName}".
    /// Default is "bbq".
    /// </remarks>
    public string QueuePrefix { get; set; } = "bbq";

    /// <summary>
    /// Gets or sets whether queues should be durable (survive broker restart).
    /// </summary>
    /// <remarks>
    /// Default is true for production reliability.
    /// </remarks>
    public bool DurableQueues { get; set; } = true;

    /// <summary>
    /// Gets or sets whether messages should be persistent (survive broker restart).
    /// </summary>
    /// <remarks>
    /// Default is true for production reliability.
    /// </remarks>
    public bool PersistentMessages { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to automatically delete queues when the last consumer disconnects.
    /// </summary>
    /// <remarks>
    /// Default is false.
    /// </remarks>
    public bool AutoDeleteQueues { get; set; }

    /// <summary>
    /// Gets or sets the JSON serializer options for event data.
    /// </summary>
    /// <remarks>
    /// If not provided, defaults to camelCase property naming.
    /// </remarks>
    public JsonSerializerOptions? JsonSerializerOptions { get; set; }
}
