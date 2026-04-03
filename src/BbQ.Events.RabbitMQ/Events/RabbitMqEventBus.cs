// -------------------------------
// RabbitMQ Event Bus Implementation
// -------------------------------
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using BbQ.Events.Events;
using BbQ.Events.RabbitMQ.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BbQ.Events.RabbitMQ.Events;

/// <summary>
/// RabbitMQ implementation of IEventBus for distributed pub/sub messaging.
/// 
/// This implementation provides a distributed event bus suitable for
/// multi-process and multi-service applications. Events are published to
/// a RabbitMQ topic exchange and delivered to all subscribers across processes.
/// </summary>
/// <remarks>
/// Features:
/// - Distributed event publishing and subscription via RabbitMQ
/// - Thread-safe event publishing and subscription
/// - Automatic cleanup of cancelled subscriptions
/// - Support for multiple concurrent subscribers across processes
/// - JSON serialization for cross-process compatibility
/// - Durable queues and persistent messages for reliability
/// - Local IEventHandler execution on publish (same as InMemoryEventBus)
/// 
/// Architecture:
/// - Uses a single topic exchange for all event types
/// - Routing key is the event type full name
/// - Each subscriber gets its own queue bound to the exchange
/// - Messages are serialized as JSON with UTF-8 encoding
/// 
/// For single-process applications, consider using the InMemoryEventBus instead.
/// </remarks>
internal sealed class RabbitMqEventBus : IEventBus, IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMqEventBus> _logger;
    private readonly RabbitMqEventBusOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ConnectionFactory _connectionFactory;

    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly SemaphoreSlim _publishChannelLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _publishChannel;
    private bool _exchangeDeclared;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the RabbitMqEventBus.
    /// </summary>
    /// <param name="serviceProvider">Service provider for resolving event handlers</param>
    /// <param name="logger">Logger for diagnostic messages</param>
    /// <param name="options">RabbitMQ configuration options</param>
    public RabbitMqEventBus(
        IServiceProvider serviceProvider,
        ILogger<RabbitMqEventBus> logger,
        RabbitMqEventBusOptions options)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _jsonOptions = _options.JsonSerializerOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        _connectionFactory = CreateConnectionFactory();
    }

    /// <summary>
    /// Publishes an event to all registered local handlers and to RabbitMQ.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to publish</typeparam>
    /// <param name="event">The event instance to publish</param>
    /// <param name="ct">Cancellation token for async operations</param>
    /// <returns>A task that completes when the event has been published</returns>
    public async Task Publish<TEvent>(TEvent @event, CancellationToken ct = default)
    {
        if (@event == null)
            throw new ArgumentNullException(nameof(@event));

        var eventType = typeof(TEvent);

        _logger.LogDebug("Publishing event of type {EventType} to RabbitMQ", eventType.Name);

        // Execute all registered local event handlers
        await ExecuteHandlers(@event, ct).ConfigureAwait(false);

        // Publish to RabbitMQ exchange
        await PublishToRabbitMqAsync(@event, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Subscribes to a stream of events of the specified type via RabbitMQ.
    /// </summary>
    /// <typeparam name="TEvent">The type of events to subscribe to</typeparam>
    /// <param name="ct">Cancellation token for terminating the subscription</param>
    /// <returns>An asynchronous stream of events</returns>
    public async IAsyncEnumerable<TEvent> Subscribe<TEvent>(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var eventType = typeof(TEvent);
        var routingKey = GetRoutingKey(eventType);

        _logger.LogDebug("Creating RabbitMQ subscription for event type {EventType}", eventType.Name);

        var connection = await EnsureConnectionAsync(ct).ConfigureAwait(false);
        var channel = await connection.CreateChannelAsync(cancellationToken: ct).ConfigureAwait(false);

        try
        {
            await EnsureExchangeAsync(channel, ct).ConfigureAwait(false);

            // Create a queue for this subscriber
            var queueName = $"{_options.QueuePrefix}.{eventType.Name}.{Guid.NewGuid():N}";
            var durable = _options.DurableQueues;
            // Non-durable queues are exclusive (tied to this connection only)
            var exclusive = !durable;
            var autoDelete = exclusive || _options.AutoDeleteQueues;

            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: durable,
                exclusive: exclusive,
                autoDelete: autoDelete,
                arguments: null,
                cancellationToken: ct).ConfigureAwait(false);

            await channel.QueueBindAsync(
                queue: queueName,
                exchange: _options.ExchangeName,
                routingKey: routingKey,
                arguments: null,
                cancellationToken: ct).ConfigureAwait(false);

            // Create a bounded channel to bridge RabbitMQ consumer and IAsyncEnumerable
            var bridge = Channel.CreateBounded<TEvent>(new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (sender, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);
                    var deserializedEvent = JsonSerializer.Deserialize<TEvent>(json, _jsonOptions);

                    if (deserializedEvent != null)
                    {
                        await bridge.Writer.WriteAsync(deserializedEvent, ct).ConfigureAwait(false);
                    }

                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Subscription is being cancelled
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error processing RabbitMQ message for event type {EventType}",
                        eventType.Name);

                    try
                    {
                        // Reject without requeue — failed messages are discarded to avoid poison-message loops
                        await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, ct).ConfigureAwait(false);
                    }
                    catch (Exception nackEx)
                    {
                        _logger.LogDebug(nackEx, "Error sending Nack for delivery tag {DeliveryTag}", ea.DeliveryTag);
                    }
                }
            };

            await channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: ct).ConfigureAwait(false);

            _logger.LogDebug(
                "RabbitMQ subscription active for event type {EventType} on queue {QueueName}",
                eventType.Name, queueName);

            await foreach (var @event in bridge.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                yield return @event;
            }
        }
        finally
        {
            try
            {
                await channel.CloseAsync(cancellationToken: CancellationToken.None).ConfigureAwait(false);
                channel.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex,
                    "Error closing RabbitMQ channel for event type {EventType}",
                    typeof(TEvent).Name);
            }

            _logger.LogDebug("RabbitMQ subscription terminated for event type {EventType}", eventType.Name);
        }
    }

    /// <summary>
    /// Disposes the RabbitMQ connection and channels.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            if (_publishChannel != null)
            {
                await _publishChannel.CloseAsync(cancellationToken: CancellationToken.None).ConfigureAwait(false);
                _publishChannel.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error closing RabbitMQ publish channel");
        }

        try
        {
            if (_connection != null)
            {
                await _connection.CloseAsync(cancellationToken: CancellationToken.None).ConfigureAwait(false);
                _connection.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error closing RabbitMQ connection");
        }

        _connectionLock.Dispose();
        _publishChannelLock.Dispose();
    }

    private ConnectionFactory CreateConnectionFactory()
    {
        var factory = new ConnectionFactory();

        if (!string.IsNullOrWhiteSpace(_options.ConnectionUri))
        {
            factory.Uri = new Uri(_options.ConnectionUri);
        }
        else
        {
            factory.HostName = _options.HostName;
            factory.Port = _options.Port;
            factory.UserName = _options.UserName;
            factory.Password = _options.Password;
            factory.VirtualHost = _options.VirtualHost;
        }

        return factory;
    }

    private async Task<IConnection> EnsureConnectionAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true })
            return _connection;

        await _connectionLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_connection is { IsOpen: true })
                return _connection;

            _logger.LogDebug("Creating RabbitMQ connection to {HostName}", _options.HostName);
            _connection = await _connectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            _exchangeDeclared = false;
            _publishChannel = null;
            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task<IChannel> EnsurePublishChannelAsync(CancellationToken ct)
    {
        if (_publishChannel is { IsOpen: true })
            return _publishChannel;

        await _connectionLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_publishChannel is { IsOpen: true })
                return _publishChannel;

            // Inline connection creation to avoid re-entrant lock
            if (_connection is not { IsOpen: true })
            {
                _logger.LogDebug("Creating RabbitMQ connection to {HostName}", _options.HostName);
                _connection = await _connectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
                _exchangeDeclared = false;
                _publishChannel = null;
            }

            _publishChannel = await _connection.CreateChannelAsync(cancellationToken: ct).ConfigureAwait(false);
            return _publishChannel;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task EnsureExchangeAsync(IChannel channel, CancellationToken ct)
    {
        if (_exchangeDeclared)
            return;

        await channel.ExchangeDeclareAsync(
            exchange: _options.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct).ConfigureAwait(false);

        _exchangeDeclared = true;

        _logger.LogDebug("RabbitMQ exchange '{ExchangeName}' declared", _options.ExchangeName);
    }

    private async Task PublishToRabbitMqAsync<TEvent>(TEvent @event, CancellationToken ct)
    {
        await _publishChannelLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var channel = await EnsurePublishChannelAsync(ct).ConfigureAwait(false);
            await EnsureExchangeAsync(channel, ct).ConfigureAwait(false);

            var eventType = typeof(TEvent);
            var routingKey = GetRoutingKey(eventType);
            var json = JsonSerializer.Serialize(@event, _jsonOptions);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = new BasicProperties
            {
                ContentType = RabbitMqConstants.JsonContentType,
                DeliveryMode = _options.PersistentMessages
                    ? DeliveryModes.Persistent
                    : DeliveryModes.Transient,
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                Type = eventType.FullName ?? eventType.Name,
            };
            properties.Headers = new Dictionary<string, object?>
            {
                [RabbitMqConstants.EventTypeHeader] = eventType.FullName ?? eventType.Name
            };

            await channel.BasicPublishAsync(
                exchange: _options.ExchangeName,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: ct).ConfigureAwait(false);

            _logger.LogDebug(
                "Event of type {EventType} published to RabbitMQ exchange '{ExchangeName}' with routing key '{RoutingKey}'",
                eventType.Name, _options.ExchangeName, routingKey);
        }
        finally
        {
            _publishChannelLock.Release();
        }
    }

    private Task ExecuteHandlers<TEvent>(TEvent @event, CancellationToken ct)
    {
        Task? singleTask = null;
        List<Task>? tasks = null;
        var handlerCount = 0;

        foreach (var handler in _serviceProvider.GetServices<IEventHandler<TEvent>>())
        {
            handlerCount++;

            var task = ExecuteHandlerSafely(handler, @event, ct);
            if (task.IsCompletedSuccessfully)
            {
                continue;
            }

            if (singleTask == null)
            {
                singleTask = task;
                continue;
            }

            tasks ??= new List<Task> { singleTask };
            tasks.Add(task);
        }

        if (handlerCount == 0)
        {
            _logger.LogDebug("No local handlers registered for event type {EventType}", typeof(TEvent).Name);
            return Task.CompletedTask;
        }

        _logger.LogDebug("Executing {HandlerCount} local handler(s) for event type {EventType}",
            handlerCount, typeof(TEvent).Name);

        if (singleTask == null)
        {
            return Task.CompletedTask;
        }

        if (tasks == null)
        {
            return singleTask;
        }

        return Task.WhenAll(tasks);
    }

    private Task ExecuteHandlerSafely<TEvent>(IEventHandler<TEvent> handler, TEvent @event, CancellationToken ct)
    {
        try
        {
            var task = handler.Handle(@event, ct);
            if (task.IsCompletedSuccessfully)
            {
                return Task.CompletedTask;
            }

            return AwaitHandler(task, handler.GetType().Name, typeof(TEvent).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error executing event handler {HandlerType} for event type {EventType}",
                handler.GetType().Name, typeof(TEvent).Name);
            return Task.CompletedTask;
        }
    }

    private async Task AwaitHandler(Task handlerTask, string handlerTypeName, string eventTypeName)
    {
        try
        {
            await handlerTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error executing event handler {HandlerType} for event type {EventType}",
                handlerTypeName,
                eventTypeName);
        }
    }

    private static string GetRoutingKey(Type eventType)
    {
        return eventType.FullName ?? eventType.Name;
    }
}
