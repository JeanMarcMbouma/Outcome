using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Channels;

namespace BbQ.Events;

/// <summary>
/// Default implementation of the projection engine with partition support.
/// 
/// This engine processes events with support for partitioned parallel processing,
/// checkpointing, and graceful shutdown.
/// </summary>
/// <remarks>
/// This implementation:
/// - Processes events from live event streams
/// - Supports partitioned projections with parallel processing across partitions
/// - Maintains sequential ordering within each partition
/// - Implements checkpoint loading and batched persistence
/// - Respects MaxDegreeOfParallelism for parallelism control
/// - Provides graceful shutdown with checkpoint flushing
/// - Dispatches to all registered handlers for each event type
/// - Logs errors but continues processing other handlers
/// - Creates a DI scope per event for handler resolution and processing
/// </remarks>
internal class DefaultProjectionEngine : IProjectionEngine
{
    private const string DefaultPartitionKey = "_default";
    
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventBus _eventBus;
    private readonly IProjectionCheckpointStore _checkpointStore;
    private readonly IProjectionMonitor? _monitor;
    private readonly ILogger<DefaultProjectionEngine> _logger;
    
    // Cache reflection calls for performance
    private static readonly ConcurrentDictionary<Type, ReflectionCache> _reflectionCache = new();
    
    // Track partition workers: (projectionName, partitionKey) -> worker task
    private readonly ConcurrentDictionary<string, PartitionWorker> _partitionWorkers = new();
    
    // Semaphore for controlling parallelism across all partitions
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _projectionSemaphores = new();

    public DefaultProjectionEngine(
        IServiceProvider serviceProvider,
        IEventBus eventBus,
        IProjectionCheckpointStore checkpointStore,
        ILogger<DefaultProjectionEngine> logger,
        IProjectionMonitor? monitor)
    {
        _serviceProvider = serviceProvider;
        _eventBus = eventBus;
        _checkpointStore = checkpointStore;
        _monitor = monitor;
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
            _logger.LogInformation("Projection engine stopping gracefully...");
            
            // Await all partition workers to complete
            await GracefulShutdownAsync();
            
            _logger.LogInformation("Projection engine stopped gracefully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Projection engine encountered an error");
            
            // Attempt graceful shutdown even on error
            await GracefulShutdownAsync();
            
            throw;
        }
    }

    /// <summary>
    /// Gracefully shuts down all partition workers, completing channels and flushing checkpoints.
    /// </summary>
    private async Task GracefulShutdownAsync()
    {
        _logger.LogInformation("Shutting down {WorkerCount} partition worker(s)", _partitionWorkers.Count);
        
        // Complete all channels to signal workers to stop
        foreach (var worker in _partitionWorkers.Values)
        {
            worker.Channel.Writer.Complete();
        }
        
        // Wait for all workers to complete
        var workerTasks = _partitionWorkers.Values.Select(w => w.Task).ToList();
        await Task.WhenAll(workerTasks);
        
        _logger.LogInformation("All partition workers stopped");
        
        // Dispose semaphores
        foreach (var semaphore in _projectionSemaphores.Values)
        {
            semaphore.Dispose();
        }
        _projectionSemaphores.Clear();
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
                
                // Get the concrete type for options
                var registration = ProjectionHandlerRegistry.GetHandlerRegistration(eventType, handlerServiceType);
                if (registration == null)
                {
                    _logger.LogWarning("No registration found for {HandlerType}", handlerServiceType.Name);
                    continue;
                }

                // Check if it's a partitioned projection handler
                if (cache.PartitionedHandlerInterface.IsAssignableFrom(handlerServiceType))
                {
                    // Get projection options
                    var options = GetProjectionOptions(registration.ConcreteType);
                    
                    // Extract and validate partition key
                    var partitionKeyObj = cache.GetPartitionKeyMethod!.Invoke(handler, new[] { @event });
                    if (partitionKeyObj is not string partitionKey || string.IsNullOrEmpty(partitionKey))
                    {
                        throw new InvalidOperationException(
                            $"Projection handler '{handlerServiceType.Name}' returned an invalid partition key for event type '{eventType.Name}'. " +
                            "Partition keys must be non-null, non-empty strings.");
                    }
                    
                    // Route event to partition worker
                    await RouteToPartitionWorkerAsync(
                        handlerServiceType,
                        eventType,
                        @event,
                        partitionKey,
                        options,
                        cache,
                        ct);
                }
                // Check if it's a regular projection handler
                else if (cache.RegularHandlerInterface.IsAssignableFrom(handlerServiceType))
                {
                    // Regular handlers use default partition (sequential processing)
                    var options = GetProjectionOptions(registration.ConcreteType);
                    
                    // Route to default partition worker
                    await RouteToPartitionWorkerAsync(
                        handlerServiceType,
                        eventType,
                        @event,
                        DefaultPartitionKey,
                        options,
                        cache,
                        ct);
                }

                _logger.LogDebug("Successfully routed {EventType} to {HandlerType}", 
                    eventType.Name, handlerServiceType.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error routing {EventType} to {HandlerType}", 
                    eventType.Name, handlerServiceType.Name);
                // Continue processing other handlers
            }
        }
    }

    /// <summary>
    /// Gets projection options for a handler, checking registry first, then attribute, then defaults.
    /// </summary>
    private ProjectionOptions GetProjectionOptions(Type concreteType)
    {
        // Check if options were registered programmatically
        var registeredOptions = ProjectionHandlerRegistry.GetProjectionOptions(concreteType.Name);
        if (registeredOptions != null)
        {
            return registeredOptions;
        }
        
        // Fall back to reading from attribute
        var attribute = concreteType.GetCustomAttribute<ProjectionAttribute>();
        
        return new ProjectionOptions
        {
            ProjectionName = concreteType.Name,
            MaxDegreeOfParallelism = attribute?.MaxDegreeOfParallelism ?? 1,
            CheckpointBatchSize = attribute?.CheckpointBatchSize ?? 100,
            StartupMode = attribute?.StartupMode ?? ProjectionStartupMode.Resume
        };
    }

    /// <summary>
    /// Routes an event to the appropriate partition worker, creating it if needed.
    /// </summary>
    private async Task RouteToPartitionWorkerAsync(
        Type handlerServiceType,
        Type eventType,
        object @event,
        string partitionKey,
        ProjectionOptions options,
        ReflectionCache cache,
        CancellationToken ct)
    {
        var workerKey = $"{options.ProjectionName}:{partitionKey}";
        
        // Get or create partition worker
        var worker = _partitionWorkers.GetOrAdd(workerKey, _ =>
        {
            _logger.LogInformation(
                "Creating partition worker for projection {ProjectionName}, partition {PartitionKey}",
                options.ProjectionName,
                partitionKey);
            
            // Get or create semaphore for this projection
            var semaphore = _projectionSemaphores.GetOrAdd(
                options.ProjectionName,
                _ =>
                {
                    // Cap unlimited parallelism at a reasonable maximum (1000 concurrent workers)
                    var maxCount = options.MaxDegreeOfParallelism > 0
                        ? options.MaxDegreeOfParallelism
                        : 1000;
                    return new SemaphoreSlim(maxCount, maxCount);
                });
            
            // Create channel for this partition
            var channel = Channel.CreateUnbounded<WorkItem>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
            
            // Update worker count for monitoring
            var currentWorkerCount = _partitionWorkers.Count(w => w.Key.StartsWith(options.ProjectionName + ":"));
            _monitor?.RecordWorkerCount(options.ProjectionName, currentWorkerCount + 1);
            
            // Start worker task with CancellationToken.None - shutdown via channel completion
            var task = Task.Run(
                async () => await ProcessPartitionAsync(
                    options,
                    partitionKey,
                    channel,
                    semaphore,
                    CancellationToken.None),
                CancellationToken.None);
            
            return new PartitionWorker
            {
                Channel = channel,
                Task = task
            };
        });
        
        // Enqueue event to worker channel
        var workItem = new WorkItem
        {
            HandlerServiceType = handlerServiceType,
            EventType = eventType,
            Event = @event,
            Cache = cache
        };
        
        await worker.Channel.Writer.WriteAsync(workItem, ct);
    }

    /// <summary>
    /// Processes events for a single partition sequentially.
    /// </summary>
    private async Task ProcessPartitionAsync(
        ProjectionOptions options,
        string partitionKey,
        Channel<WorkItem> channel,
        SemaphoreSlim semaphore,
        CancellationToken ct)
    {
        var checkpointKey = $"{options.ProjectionName}:{partitionKey}";
        
        try
        {
            // Determine starting position based on startup mode
            long? checkpoint = null;
            string startupModeDescription;
            
            switch (options.StartupMode)
            {
                case ProjectionStartupMode.Resume:
                    // Load checkpoint and resume from last position
                    checkpoint = await _checkpointStore.GetCheckpointAsync(checkpointKey, ct);
                    startupModeDescription = checkpoint.HasValue 
                        ? $"Resume from checkpoint {checkpoint.Value}" 
                        : "Resume from beginning";
                    break;
                    
                case ProjectionStartupMode.Replay:
                    // Ignore checkpoint and rebuild from scratch
                    checkpoint = null;
                    startupModeDescription = "Replay from beginning";
                    // Reset the checkpoint in storage to ensure clean replay
                    await _checkpointStore.ResetCheckpointAsync(checkpointKey, ct);
                    break;
                    
                case ProjectionStartupMode.CatchUp:
                case ProjectionStartupMode.LiveOnly:
                    // Delegate starting position to the event source by not providing a checkpoint.
                    // NOTE: In a live-only event bus, this effectively means "new events only"
                    // because there are no historical events to replay. In an event-store-backed
                    // implementation, however, a null checkpoint typically means "from the
                    // beginning of the stream" rather than "from the current position".
                    // If true "start from now" semantics are required, the implementation
                    // of the event source / subscription must determine and use the current
                    // position explicitly.
                    checkpoint = null;
                    startupModeDescription = options.StartupMode == ProjectionStartupMode.CatchUp 
                        ? "CatchUp - starting near-real-time" 
                        : "LiveOnly - processing new events only";
                    break;
                    
                default:
                    throw new InvalidOperationException($"Unknown startup mode: {options.StartupMode}");
            }
            
            var eventsProcessedSinceCheckpoint = 0;
            var currentPosition = checkpoint ?? 0;
            
            _logger.LogInformation(
                "Partition worker started for {ProjectionName}:{PartitionKey} in {StartupMode} mode",
                options.ProjectionName,
                partitionKey,
                startupModeDescription);
            
            await foreach (var workItem in channel.Reader.ReadAllAsync(ct))
            {
                // Acquire semaphore before processing each event
                await semaphore.WaitAsync(ct);
                
                try
                {
                    // Process the event
                    await ProcessWorkItemAsync(workItem, ct);
                    
                    // Track position and checkpoint ONLY after successful processing
                    currentPosition++;
                    eventsProcessedSinceCheckpoint++;
                    
                    // Record event processed for monitoring
                    _monitor?.RecordEventProcessed(options.ProjectionName, partitionKey, currentPosition);
                    
                    // Persist checkpoint after batch size reached
                    if (eventsProcessedSinceCheckpoint >= options.CheckpointBatchSize)
                    {
                        await _checkpointStore.SaveCheckpointAsync(checkpointKey, currentPosition, ct);
                        
                        // Record checkpoint written for monitoring
                        _monitor?.RecordCheckpointWritten(options.ProjectionName, partitionKey, currentPosition);
                        
                        _logger.LogDebug(
                            "Checkpoint saved for {ProjectionName}:{PartitionKey} at position {Position}",
                            options.ProjectionName,
                            partitionKey,
                            currentPosition);
                        
                        eventsProcessedSinceCheckpoint = 0;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error processing event in partition {ProjectionName}:{PartitionKey}. Event will not be checkpointed.",
                        options.ProjectionName,
                        partitionKey);
                    // Do NOT increment position on failure - event should be retried on restart
                }
                finally
                {
                    // Release semaphore after processing this event
                    semaphore.Release();
                }
            }
            
            // Final checkpoint flush on shutdown
            if (eventsProcessedSinceCheckpoint > 0)
            {
                try
                {
                    await _checkpointStore.SaveCheckpointAsync(checkpointKey, currentPosition, ct);
                    
                    // Record final checkpoint written for monitoring
                    _monitor?.RecordCheckpointWritten(options.ProjectionName, partitionKey, currentPosition);
                    
                    _logger.LogInformation(
                        "Final checkpoint saved for {ProjectionName}:{PartitionKey} at position {Position}",
                        options.ProjectionName,
                        partitionKey,
                        currentPosition);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to save final checkpoint for {ProjectionName}:{PartitionKey} at position {Position}",
                        options.ProjectionName,
                        partitionKey,
                        currentPosition);
                }
            }
            
            _logger.LogInformation(
                "Partition worker stopped for {ProjectionName}:{PartitionKey}",
                options.ProjectionName,
                partitionKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Fatal error in partition worker for {ProjectionName}:{PartitionKey}",
                options.ProjectionName,
                partitionKey);
            throw;
        }
    }

    /// <summary>
    /// Processes a single work item by invoking the projection handler.
    /// </summary>
    private async Task ProcessWorkItemAsync(WorkItem workItem, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService(workItem.HandlerServiceType);
        
        // Check if it's a regular or partitioned handler and invoke appropriately
        if (workItem.Cache.RegularHandlerInterface.IsAssignableFrom(workItem.HandlerServiceType))
        {
            var projectTask = (ValueTask)workItem.Cache.RegularProjectMethod.Invoke(handler, new[] { workItem.Event, ct })!;
            await projectTask;
        }
        else if (workItem.Cache.PartitionedHandlerInterface.IsAssignableFrom(workItem.HandlerServiceType))
        {
            var projectTask = (ValueTask)workItem.Cache.PartitionedProjectMethod.Invoke(handler, new[] { workItem.Event, ct })!;
            await projectTask;
        }
    }

    /// <summary>
    /// Represents work to be processed by a partition worker.
    /// </summary>
    private class WorkItem
    {
        public Type HandlerServiceType { get; set; } = null!;
        public Type EventType { get; set; } = null!;
        public object Event { get; set; } = null!;
        public ReflectionCache Cache { get; set; } = null!;
    }

    /// <summary>
    /// Represents a partition worker with its channel and task.
    /// </summary>
    private class PartitionWorker
    {
        public Channel<WorkItem> Channel { get; set; } = null!;
        public Task Task { get; set; } = null!;
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
        public MethodInfo? GetPartitionKeyMethod { get; }

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
            
            // Cache GetPartitionKey method for partitioned handlers
            GetPartitionKeyMethod = PartitionedHandlerInterface.GetMethod(nameof(IPartitionedProjectionHandler<object>.GetPartitionKey));
        }
    }
}
