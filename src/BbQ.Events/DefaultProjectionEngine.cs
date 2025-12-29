using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;

namespace BbQ.Events;

/// <summary>
/// Default implementation of the projection engine.
/// 
/// This engine processes events sequentially (event-by-event) and dispatches them
/// to registered projection handlers.
/// </summary>
/// <remarks>
/// This implementation:
/// - Processes events sequentially from live event streams
/// - Dispatches to all registered handlers for each event type
/// - Logs errors but continues processing other handlers
/// - Uses a single DI scope per event for efficiency
/// - Does not currently implement checkpointing (infrastructure provided via IProjectionCheckpointStore)
/// - Does not currently implement parallel processing for partitioned projections
/// 
/// Batch processing, parallel processing, checkpointing, configurable retry policies,
/// and dead-letter queues can be added as needed for specific use cases.
/// </remarks>
internal class DefaultProjectionEngine : IProjectionEngine
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventBus _eventBus;
    private readonly IProjectionCheckpointStore _checkpointStore;
    private readonly ILogger<DefaultProjectionEngine> _logger;
    
    // Cache reflection calls for performance
    private static readonly ConcurrentDictionary<Type, ReflectionCache> _reflectionCache = new();

    public DefaultProjectionEngine(
        IServiceProvider serviceProvider,
        IEventBus eventBus,
        IProjectionCheckpointStore checkpointStore,
        ILogger<DefaultProjectionEngine> logger)
    {
        _serviceProvider = serviceProvider;
        _eventBus = eventBus;
        _checkpointStore = checkpointStore; // Kept for future extensibility
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

        var tasks = eventTypes
            .Select(eventType =>
            {
                var handlerServiceTypes = ProjectionHandlerRegistry.GetHandlers(eventType);
                return ProcessEventStreamAsync(eventType, handlerServiceTypes, ct);
            })
            .ToList();

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
            // Get or create cached reflection info
            var cache = _reflectionCache.GetOrAdd(eventType, type => new ReflectionCache(type));
            
            // Use reflection to call Subscribe<TEvent> on the event bus
            var eventStream = cache.SubscribeMethod.Invoke(_eventBus, new object[] { ct });
            
            if (eventStream == null)
            {
                _logger.LogError("Subscribe returned null for event type {EventType}", eventType.Name);
                return;
            }
            
            // Call GetAsyncEnumerator with the CancellationToken
            var enumerator = cache.GetEnumeratorMethod.Invoke(eventStream, new object[] { ct });
            
            if (enumerator == null)
            {
                _logger.LogError("GetAsyncEnumerator returned null for event type {EventType}", eventType.Name);
                return;
            }

            // Get MoveNextAsync and Current from the enumerator (these are instance-specific)
            var enumeratorType = enumerator.GetType();
            var moveNextMethod = enumeratorType.GetMethod("MoveNextAsync");
            var currentProperty = enumeratorType.GetProperty("Current");
            
            if (moveNextMethod == null || currentProperty == null)
            {
                _logger.LogError("Could not find MoveNextAsync or Current on enumerator for event type {EventType}", eventType.Name);
                return;
            }

            while (true)
            {
                var moveNextResult = moveNextMethod.Invoke(enumerator, Array.Empty<object>());
                if (moveNextResult == null)
                {
                    _logger.LogError("MoveNextAsync returned null for event type {EventType}", eventType.Name);
                    break;
                }
                
                var moveNextTask = (ValueTask<bool>)moveNextResult;
                if (!await moveNextTask)
                {
                    break;
                }

                var currentEvent = currentProperty.GetValue(enumerator);
                if (currentEvent != null)
                {
                    await DispatchEventToHandlersAsync(eventType, currentEvent, handlerServiceTypes, cache, ct);
                }
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

    private async Task DispatchEventToHandlersAsync(
        Type eventType, 
        object @event, 
        List<Type> handlerServiceTypes, 
        ReflectionCache cache,
        CancellationToken ct)
    {
        // Use a single scope for all handlers processing this event
        using var scope = _serviceProvider.CreateScope();
        
        foreach (var handlerServiceType in handlerServiceTypes)
        {
            try
            {
                var handler = scope.ServiceProvider.GetRequiredService(handlerServiceType);

                // Check if it's a regular projection handler
                if (cache.RegularHandlerInterface.IsAssignableFrom(handlerServiceType))
                {
                    var projectTask = (ValueTask)cache.RegularProjectMethod.Invoke(handler, new[] { @event, ct })!;
                    await projectTask;
                }
                // Check if it's a partitioned projection handler
                else if (cache.PartitionedHandlerInterface.IsAssignableFrom(handlerServiceType))
                {
                    var projectTask = (ValueTask)cache.PartitionedProjectMethod.Invoke(handler, new[] { @event, ct })!;
                    await projectTask;
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

    /// <summary>
    /// Caches reflection information for a specific event type to avoid repeated reflection lookups.
    /// </summary>
    private class ReflectionCache
    {
        public MethodInfo SubscribeMethod { get; }
        public MethodInfo GetEnumeratorMethod { get; }
        public Type RegularHandlerInterface { get; }
        public Type PartitionedHandlerInterface { get; }
        public MethodInfo RegularProjectMethod { get; }
        public MethodInfo PartitionedProjectMethod { get; }

        public ReflectionCache(Type eventType)
        {
            // Cache Subscribe<TEvent> method
            SubscribeMethod = typeof(IEventBus)
                .GetMethod(nameof(IEventBus.Subscribe))!
                .MakeGenericMethod(eventType);

            // Cache GetAsyncEnumerator method
            var asyncEnumerableType = typeof(IAsyncEnumerable<>).MakeGenericType(eventType);
            GetEnumeratorMethod = asyncEnumerableType.GetMethod("GetAsyncEnumerator")!;

            // Cache handler interface types
            RegularHandlerInterface = typeof(IProjectionHandler<>).MakeGenericType(eventType);
            PartitionedHandlerInterface = typeof(IPartitionedProjectionHandler<>).MakeGenericType(eventType);

            // Cache ProjectAsync methods
            RegularProjectMethod = RegularHandlerInterface.GetMethod(nameof(IProjectionHandler<object>.ProjectAsync))!;
            PartitionedProjectMethod = PartitionedHandlerInterface.GetMethod(nameof(IPartitionedProjectionHandler<object>.ProjectAsync))!;
        }
    }
}
