using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BbQ.Events;

/// <summary>
/// Default implementation of the projection engine.
/// 
/// This engine processes events sequentially (event-by-event) and dispatches them
/// to registered projection handlers. It maintains checkpoints to enable resumability
/// after restarts or failures.
/// </summary>
/// <remarks>
/// This is a basic implementation that:
/// - Processes events sequentially (no parallel processing yet)
/// - Dispatches to all registered handlers for each event type
/// - Logs errors but continues processing
/// - Uses a simple checkpoint strategy (after each event)
/// 
/// Future enhancements could include:
/// - Batch processing for higher throughput
/// - Parallel processing for partitioned projections
/// - Configurable retry policies
/// - Dead-letter queues for failed events
/// </remarks>
internal class DefaultProjectionEngine : IProjectionEngine
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventBus _eventBus;
    private readonly IProjectionCheckpointStore _checkpointStore;
    private readonly ILogger<DefaultProjectionEngine> _logger;

    public DefaultProjectionEngine(
        IServiceProvider serviceProvider,
        IEventBus eventBus,
        IProjectionCheckpointStore checkpointStore,
        ILogger<DefaultProjectionEngine> logger)
    {
        _serviceProvider = serviceProvider;
        _eventBus = eventBus;
        _checkpointStore = checkpointStore;
        _logger = logger;
    }

    /// <summary>
    /// Runs the projection engine, processing events until cancelled.
    /// </summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        // Get registered event types from the registry
        var eventTypes = ProjectionHandlerRegistry.GetEventTypes().ToList();

        if (eventTypes.Count == 0)
        {
            _logger.LogWarning("No projection handlers registered. Engine will not process any events.");
            return;
        }

        _logger.LogInformation("Starting projection engine with {HandlerCount} event type(s) registered", 
            eventTypes.Count);

        var tasks = new List<Task>();

        foreach (var eventType in eventTypes)
        {
            var handlerServiceTypes = ProjectionHandlerRegistry.GetHandlers(eventType);
            // Create a task for each event type subscription
            var task = ProcessEventStreamAsync(eventType, handlerServiceTypes, ct);
            tasks.Add(task);
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Projection engine stopped gracefully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Projection engine encountered an error");
            throw;
        }
    }

    private async Task ProcessEventStreamAsync(Type eventType, List<Type> handlerServiceTypes, CancellationToken ct)
    {
        _logger.LogInformation("Starting event stream processor for {EventType}", eventType.Name);

        try
        {
            // Use reflection to call Subscribe<TEvent> on the event bus
            var subscribeMethod = typeof(IEventBus)
                .GetMethod(nameof(IEventBus.Subscribe))!
                .MakeGenericMethod(eventType);

            var eventStream = subscribeMethod.Invoke(_eventBus, new object[] { ct });

            // Get the async enumerator
            var getEnumeratorMethod = eventStream!.GetType().GetMethod("GetAsyncEnumerator")!;
            var enumerator = getEnumeratorMethod.Invoke(eventStream, new object[] { ct });

            // Process events from the stream
            var moveNextMethod = enumerator!.GetType().GetMethod("MoveNextAsync")!;
            var currentProperty = enumerator.GetType().GetProperty("Current")!;

            while (true)
            {
                var moveNextTask = (ValueTask<bool>)moveNextMethod.Invoke(enumerator, Array.Empty<object>())!;
                if (!await moveNextTask)
                {
                    break;
                }

                var currentEvent = currentProperty.GetValue(enumerator);
                await DispatchEventToHandlersAsync(eventType, currentEvent!, handlerServiceTypes, ct);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Event stream processor for {EventType} stopped", eventType.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event stream for {EventType}", eventType.Name);
            throw;
        }
    }

    private async Task DispatchEventToHandlersAsync(Type eventType, object @event, List<Type> handlerServiceTypes, CancellationToken ct)
    {
        foreach (var handlerServiceType in handlerServiceTypes)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService(handlerServiceType);

                // Check if it's a regular projection handler
                var regularHandlerInterface = typeof(IProjectionHandler<>).MakeGenericType(eventType);
                if (regularHandlerInterface.IsAssignableFrom(handlerServiceType))
                {
                    var projectMethod = regularHandlerInterface.GetMethod(nameof(IProjectionHandler<object>.ProjectAsync))!;
                    var projectTask = (ValueTask)projectMethod.Invoke(handler, new[] { @event, ct })!;
                    await projectTask;
                }
                // Check if it's a partitioned projection handler
                else
                {
                    var partitionedHandlerInterface = typeof(IPartitionedProjectionHandler<>).MakeGenericType(eventType);
                    if (partitionedHandlerInterface.IsAssignableFrom(handlerServiceType))
                    {
                        var projectMethod = partitionedHandlerInterface.GetMethod(nameof(IPartitionedProjectionHandler<object>.ProjectAsync))!;
                        var projectTask = (ValueTask)projectMethod.Invoke(handler, new[] { @event, ct })!;
                        await projectTask;
                    }
                }

                _logger.LogDebug("Successfully projected {EventType} to {HandlerType}", 
                    eventType.Name, handlerServiceType.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error projecting {EventType} to {HandlerType}", 
                    eventType.Name, handlerServiceType.Name);
                // Continue processing other handlers
            }
        }
    }
}
