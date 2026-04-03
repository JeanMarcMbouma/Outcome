namespace BbQ.Events.RabbitMQ.Internal;

/// <summary>
/// Internal constants used by the RabbitMQ event bus implementation.
/// </summary>
internal static class RabbitMqConstants
{
    /// <summary>
    /// The default exchange name for event publishing.
    /// </summary>
    public const string DefaultExchangeName = "bbq.events";

    /// <summary>
    /// The default queue name prefix.
    /// </summary>
    public const string DefaultQueuePrefix = "bbq";

    /// <summary>
    /// The content type header value for JSON messages.
    /// </summary>
    public const string JsonContentType = "application/json";

    /// <summary>
    /// The header name for the event type.
    /// </summary>
    public const string EventTypeHeader = "bbq-event-type";
}
