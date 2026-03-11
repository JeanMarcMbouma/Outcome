using BbQ.Events.Checkpointing;
using BbQ.Events.Events;
using BbQ.Events.Projections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Channels;

namespace BbQ.Events.Engine;

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
    
    // Track worker count per projection for efficient monitoring
    private readonly ConcurrentDictionary<string, int> _workerCountByProjection = new();
    
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

            MethodInfo? moveNextMethod = GetMoveNextAsync(enumeratorType);
            var currentProperty = GetCurrentAsync(enumeratorType);


            if (moveNextMethod == null || currentProperty == null)
            {
                _logger.LogError("Could not find MoveNextAsync or Current on enumerator for event type {EventType}", eventType.Name);
                return;
            }

            while (true)
            {
                var moveNextResult = moveNextMethod!.Invoke(enumerator, Array.Empty<object>());
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

    private static MethodInfo? GetMoveNextAsync(Type? type)
    {
        if (type == null)
            return null;
        // Look for method named "MoveNextAsync"
        var method = type.GetMethod("MoveNextAsync", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method != null)
        {
            return method;
        }
        else
        {

            // Check interfaces for explicit implementation
            foreach (var iface in type.GetInterfaces())
            {
                var map = type.GetInterfaceMap(iface);
                for (int i = 0; i < map.InterfaceMethods.Length; i++)
                {
                    if (map.InterfaceMethods[i].Name == "MoveNextAsync")
                    {
                        return map.TargetMethods[i];
                    }
                }
            }
        }

        return null;
    }

    private static PropertyInfo? GetCurrentAsync(Type? type)
    {
        if (type == null) 
            return null;
        // Look for method named "MoveNextAsync"
        var method = type.GetProperty("Current", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method != null)
        {
            return method;
        }
        else
        {

            // Check interfaces for explicit implementation
            foreach (var iface in type.GetInterfaces())
            {
                var map = type.GetInterfaceMap(iface);
                for (int i = 0; i < map.InterfaceMethods.Length; i++)
                {
                    if (map.InterfaceMethods[i].Name == "get_Current")
                    {
                        // The target method is the getter; get its associated property
                        var getter = map.TargetMethods[i];

                        // Find the property that uses this getter
                        var props = type.GetProperties(
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                        );

                        foreach (var p in props)
                        {
                            if (p.GetMethod == getter)
                                return p;
                        }

                        // If no property wrapper exists, return a synthetic PropertyInfo? (rare)
                        return null;
                    }

                }
            }
        }

        return null;
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
                // Check if it's a batch projection handler
                else if (cache.BatchHandlerInterface.IsAssignableFrom(handlerServiceType))
                {
                    var options = GetProjectionOptions(registration.ConcreteType);
                    
                    // Route to default partition worker (batch collection happens inside partition worker)
                    await RouteToPartitionWorkerAsync(
                        handlerServiceType,
                        eventType,
                        @event,
                        DefaultPartitionKey,
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
    private static ProjectionOptions GetProjectionOptions(Type concreteType)
    {
        // Check if options were registered programmatically
        var registeredOptions = ProjectionHandlerRegistry.GetProjectionOptions(concreteType.Name);
        if (registeredOptions != null)
        {
            return registeredOptions;
        }
        
        // Fall back to reading from attribute
        var attribute = concreteType.GetCustomAttribute<ProjectionAttribute>();
        
        var options = new ProjectionOptions
        {
            MaxDegreeOfParallelism = attribute?.MaxDegreeOfParallelism ?? 1,
            CheckpointBatchSize = attribute?.CheckpointBatchSize ?? 100,
            StartupMode = attribute?.StartupMode ?? ProjectionStartupMode.Resume,
            ChannelCapacity = attribute?.ChannelCapacity ?? 1000,
            BackpressureStrategy = attribute?.BackpressureStrategy ?? BackpressureStrategy.Block,
            BatchSize = attribute?.BatchSize ?? 0,
            // ErrorHandling is already initialized by property initializer to default values
        };
        
        // Use ProjectionNameResolver for consistent name resolution
        // Since ProjectionName is not explicitly set in options, the resolver will use the type name
        options.ProjectionName = ProjectionNameResolver.Resolve(concreteType, options);
        
        return options;
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
            
            // Create channel for this partition with backpressure control
            var channel = CreateChannelWithBackpressure<WorkItem>(options);
            
            // Increment worker count for this projection
            // Note: Worker count only grows as new partitions are discovered
            // since partition workers are long-lived and persist until engine shutdown
            var currentWorkerCount = _workerCountByProjection.AddOrUpdate(
                options.ProjectionName,
                1,
                (_, count) => count + 1);
            _monitor?.RecordWorkerCount(options.ProjectionName, currentWorkerCount);
            
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
        
        // Enqueue event to worker channel with backpressure handling
        var workItem = new WorkItem
        {
            HandlerServiceType = handlerServiceType,
            EventType = eventType,
            Event = @event,
            Cache = cache
        };
        
        await WriteToChannelWithBackpressureAsync(worker.Channel, workItem, options, partitionKey, ct);
    }

    /// <summary>
    /// Creates a channel with the configured backpressure strategy.
    /// </summary>
    private static Channel<T> CreateChannelWithBackpressure<T>(ProjectionOptions options)
    {
        return options.BackpressureStrategy switch
        {
            BackpressureStrategy.Block => Channel.CreateBounded<T>(new BoundedChannelOptions(options.ChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            }),
            BackpressureStrategy.DropNewest => Channel.CreateBounded<T>(new BoundedChannelOptions(options.ChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false
            }),
            BackpressureStrategy.DropOldest => Channel.CreateBounded<T>(new BoundedChannelOptions(options.ChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            }),
            _ => throw new InvalidOperationException($"Unknown backpressure strategy: {options.BackpressureStrategy}")
        };
    }

    /// <summary>
    /// Writes to a channel with backpressure handling and monitoring.
    /// </summary>
    private async Task WriteToChannelWithBackpressureAsync<T>(
        Channel<T> channel,
        T item,
        ProjectionOptions options,
        string partitionKey,
        CancellationToken ct)
    {
        // Use different write strategies based on backpressure mode
        if (options.BackpressureStrategy == BackpressureStrategy.Block)
        {
            // Block mode: use WriteAsync which waits for space
            await channel.Writer.WriteAsync(item, ct);
        }
        else
        {
            // Drop modes: use TryWrite which drops immediately if full
            if (!channel.Writer.TryWrite(item))
            {
                _logger.LogWarning(
                    "Event dropped for projection {ProjectionName}:{PartitionKey} due to backpressure (strategy: {Strategy}, capacity: {Capacity})",
                    options.ProjectionName,
                    partitionKey,
                    options.BackpressureStrategy,
                    options.ChannelCapacity);
                
                _monitor?.RecordEventDropped(options.ProjectionName, partitionKey);
            }
        }
        
        // Record queue depth for monitoring
        var queueDepth = channel.Reader.Count;
        _monitor?.RecordQueueDepth(options.ProjectionName, partitionKey, queueDepth);
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
            
            // Determine if this partition is used for batch processing
            var isBatchMode = options.BatchSize > 0;
            
            if (isBatchMode)
            {
                await ProcessPartitionBatchModeAsync(
                    options, partitionKey, checkpointKey, channel, semaphore,
                    currentPosition, eventsProcessedSinceCheckpoint, ct);
            }
            else
            {
                await ProcessPartitionSingleModeAsync(
                    options, partitionKey, checkpointKey, channel, semaphore,
                    currentPosition, eventsProcessedSinceCheckpoint, ct);
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
    /// Processes events one-at-a-time (original behavior for regular and partitioned handlers).
    /// </summary>
    private async Task ProcessPartitionSingleModeAsync(
        ProjectionOptions options,
        string partitionKey,
        string checkpointKey,
        Channel<WorkItem> channel,
        SemaphoreSlim semaphore,
        long currentPosition,
        int eventsProcessedSinceCheckpoint,
        CancellationToken ct)
    {
        await foreach (var workItem in channel.Reader.ReadAllAsync(ct))
        {
            await semaphore.WaitAsync(ct);
            
            try
            {
                var shouldContinue = await ProcessWorkItemWithErrorHandlingAsync(
                    workItem, options, partitionKey, currentPosition, ct);
                
                if (!shouldContinue)
                {
                    _logger.LogWarning(
                        "Stopping projection worker for {ProjectionName}:{PartitionKey} due to error handling policy",
                        options.ProjectionName, partitionKey);
                    return;
                }
                
                currentPosition++;
                eventsProcessedSinceCheckpoint++;
                
                _monitor?.RecordEventProcessed(options.ProjectionName, partitionKey, currentPosition);
                _monitor?.RecordQueueDepth(options.ProjectionName, partitionKey, channel.Reader.Count);
                
                if (eventsProcessedSinceCheckpoint >= options.CheckpointBatchSize)
                {
                    await _checkpointStore.SaveCheckpointAsync(checkpointKey, currentPosition, ct);
                    _monitor?.RecordCheckpointWritten(options.ProjectionName, partitionKey, currentPosition);
                    
                    _logger.LogDebug(
                        "Checkpoint saved for {ProjectionName}:{PartitionKey} at position {Position}",
                        options.ProjectionName, partitionKey, currentPosition);
                    
                    eventsProcessedSinceCheckpoint = 0;
                }
            }
            finally
            {
                semaphore.Release();
            }
        }
        
        // Final checkpoint flush on shutdown
        await FlushFinalCheckpointAsync(options, partitionKey, checkpointKey, currentPosition, eventsProcessedSinceCheckpoint, ct);
    }

    /// <summary>
    /// Processes events in configurable batches for <see cref="Projections.IProjectionBatchHandler{TEvent}"/> handlers.
    /// Events are collected until <see cref="ProjectionOptions.BatchSize"/> is reached or
    /// <see cref="ProjectionOptions.BatchTimeout"/> expires, then dispatched as a batch.
    /// </summary>
    private async Task ProcessPartitionBatchModeAsync(
        ProjectionOptions options,
        string partitionKey,
        string checkpointKey,
        Channel<WorkItem> channel,
        SemaphoreSlim semaphore,
        long currentPosition,
        int eventsProcessedSinceCheckpoint,
        CancellationToken ct)
    {
        var batch = new List<WorkItem>(options.BatchSize);
        var batchTimer = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInformation(
            "Batch partition worker started for {ProjectionName}:{PartitionKey} (BatchSize={BatchSize}, BatchTimeout={BatchTimeout}ms, AutoCheckpoint={AutoCheckpoint})",
            options.ProjectionName, partitionKey,
            options.BatchSize, options.BatchTimeout.TotalMilliseconds, options.AutoCheckpoint);

        while (await WaitToReadWithTimeoutAsync(channel.Reader, options.BatchTimeout, batchTimer, ct))
        {
            while (channel.Reader.TryRead(out var workItem))
            {
                batch.Add(workItem);

                if (batch.Count >= options.BatchSize)
                    break;
            }

            var batchFull = batch.Count >= options.BatchSize;
            var timeoutExpired = batchTimer.Elapsed >= options.BatchTimeout;

            if ((batchFull || timeoutExpired) && batch.Count > 0)
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    var shouldContinue = await ProcessBatchWithErrorHandlingAsync(
                        batch, options, partitionKey, currentPosition, ct);

                    if (!shouldContinue)
                    {
                        _logger.LogWarning(
                            "Stopping projection worker for {ProjectionName}:{PartitionKey} due to error handling policy",
                            options.ProjectionName, partitionKey);
                        return;
                    }

                    var batchCount = batch.Count;
                    currentPosition += batchCount;
                    eventsProcessedSinceCheckpoint += batchCount;

                    _monitor?.RecordEventProcessed(options.ProjectionName, partitionKey, currentPosition);
                    _monitor?.RecordQueueDepth(options.ProjectionName, partitionKey, channel.Reader.Count);

                    if (options.AutoCheckpoint)
                    {
                        await _checkpointStore.SaveCheckpointAsync(checkpointKey, currentPosition, ct);
                        _monitor?.RecordCheckpointWritten(options.ProjectionName, partitionKey, currentPosition);

                        _logger.LogDebug(
                            "Checkpoint saved for {ProjectionName}:{PartitionKey} at position {Position} after batch of {BatchCount}",
                            options.ProjectionName, partitionKey, currentPosition, batchCount);
                        
                        eventsProcessedSinceCheckpoint = 0;
                    }
                    else if (eventsProcessedSinceCheckpoint >= options.CheckpointBatchSize)
                    {
                        await _checkpointStore.SaveCheckpointAsync(checkpointKey, currentPosition, ct);
                        _monitor?.RecordCheckpointWritten(options.ProjectionName, partitionKey, currentPosition);
                        eventsProcessedSinceCheckpoint = 0;
                    }
                }
                finally
                {
                    semaphore.Release();
                }

                batch.Clear();
                batchTimer.Restart();
            }
        }

        // Flush remaining events on shutdown
        if (batch.Count > 0)
        {
            _logger.LogInformation(
                "Flushing final batch of {BatchCount} events for {ProjectionName}:{PartitionKey}",
                batch.Count, options.ProjectionName, partitionKey);

            await semaphore.WaitAsync(ct);
            try
            {
                var shouldContinue = await ProcessBatchWithErrorHandlingAsync(
                    batch, options, partitionKey, currentPosition, ct);

                if (shouldContinue)
                {
                    currentPosition += batch.Count;
                    eventsProcessedSinceCheckpoint += batch.Count;
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        await FlushFinalCheckpointAsync(options, partitionKey, checkpointKey, currentPosition, eventsProcessedSinceCheckpoint, ct);
    }

    /// <summary>
    /// Waits for data in the channel reader, respecting batch timeout.
    /// Returns false when the channel is completed (no more data).
    /// </summary>
    private static async Task<bool> WaitToReadWithTimeoutAsync(
        ChannelReader<WorkItem> reader,
        TimeSpan batchTimeout,
        System.Diagnostics.Stopwatch batchTimer,
        CancellationToken ct)
    {
        var remaining = batchTimeout - batchTimer.Elapsed;
        if (remaining <= TimeSpan.Zero)
        {
            // Timeout already expired; check if there's data or channel completed
            return !reader.Completion.IsCompleted;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(remaining);
        try
        {
            return await reader.WaitToReadAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Batch timeout expired, not external cancellation – check if channel is still open
            return !reader.Completion.IsCompleted;
        }
    }

    /// <summary>
    /// Processes a batch of work items by invoking the batch handler.
    /// </summary>
    private async Task<bool> ProcessBatchWithErrorHandlingAsync(
        List<WorkItem> batch,
        ProjectionOptions options,
        string partitionKey,
        long currentPosition,
        CancellationToken ct)
    {
        var errorHandling = options.ErrorHandling;
        errorHandling.Validate();

        if (errorHandling.Strategy != ProjectionErrorHandlingStrategy.Retry)
        {
            try
            {
                await InvokeBatchHandlerAsync(batch, ct);
                return true;
            }
            catch (Exception ex)
            {
                return HandleBatchError(ex, options, partitionKey, currentPosition, batch.Count, errorHandling.Strategy, 1);
            }
        }

        // Retry strategy
        var attempt = 0;
        var delay = errorHandling.InitialRetryDelayMs;

        while (attempt < errorHandling.MaxRetryAttempts)
        {
            try
            {
                await InvokeBatchHandlerAsync(batch, ct);

                if (attempt > 0)
                {
                    _logger.LogInformation(
                        "Successfully processed batch for {ProjectionName}:{PartitionKey} at position {Position} after {Attempts} retry attempt(s)",
                        options.ProjectionName, partitionKey, currentPosition, attempt);
                }

                return true;
            }
            catch (Exception ex)
            {
                attempt++;

                if (attempt >= errorHandling.MaxRetryAttempts)
                {
                    _logger.LogError(
                        ex,
                        "Failed to process batch for {ProjectionName}:{PartitionKey} at position {Position} after {MaxAttempts} attempts. Fallback: {FallbackStrategy}",
                        options.ProjectionName, partitionKey, currentPosition,
                        errorHandling.MaxRetryAttempts, errorHandling.FallbackStrategy);

                    return HandleBatchError(ex, options, partitionKey, currentPosition, batch.Count, errorHandling.FallbackStrategy, attempt);
                }

                _logger.LogWarning(
                    ex,
                    "Error processing batch for {ProjectionName}:{PartitionKey} at position {Position}. Attempt {Attempt} of {MaxAttempts}. Retrying in {DelayMs}ms",
                    options.ProjectionName, partitionKey, currentPosition,
                    attempt, errorHandling.MaxRetryAttempts, delay);

                try
                {
                    await Task.Delay(delay, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }

                delay = Math.Min(delay * 2, errorHandling.MaxRetryDelayMs);
            }
        }

        return false;
    }

    /// <summary>
    /// Handles a batch processing error according to the specified strategy.
    /// </summary>
    private bool HandleBatchError(
        Exception ex,
        ProjectionOptions options,
        string partitionKey,
        long currentPosition,
        int batchCount,
        ProjectionErrorHandlingStrategy strategy,
        int totalAttempts)
    {
        switch (strategy)
        {
            case ProjectionErrorHandlingStrategy.Skip:
                _logger.LogError(
                    ex,
                    "Skipping failed batch of {BatchCount} events for {ProjectionName}:{PartitionKey} at position {Position} after {TotalAttempts} attempt(s)",
                    batchCount, options.ProjectionName, partitionKey, currentPosition, totalAttempts);
                return true;

            case ProjectionErrorHandlingStrategy.Stop:
                _logger.LogCritical(
                    ex,
                    "Stopping projection for {ProjectionName}:{PartitionKey} at position {Position} after {TotalAttempts} attempt(s). Batch size: {BatchCount}",
                    options.ProjectionName, partitionKey, currentPosition, totalAttempts, batchCount);
                return false;

            default:
                _logger.LogError(
                    ex,
                    "Unknown strategy {Strategy} for {ProjectionName}. Stopping.",
                    strategy, options.ProjectionName);
                return false;
        }
    }

    /// <summary>
    /// Invokes the batch handler with a typed list of events.
    /// </summary>
    private async Task InvokeBatchHandlerAsync(List<WorkItem> batch, CancellationToken ct)
    {
        if (batch.Count == 0) return;

        // All work items in a partition share the same handler service type and cache
        var firstItem = batch[0];
        var cache = firstItem.Cache;

        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService(firstItem.HandlerServiceType);

        // Create typed IReadOnlyList<TEvent>
        var typedList = cache.CreateTypedBatchList(batch.Select(w => w.Event));

        var projectTask = (ValueTask)cache.ProjectBatchMethod!.Invoke(handler, new[] { typedList, ct })!;
        await projectTask;
    }

    /// <summary>
    /// Persists a final checkpoint on shutdown if there are unwritten events.
    /// </summary>
    private async Task FlushFinalCheckpointAsync(
        ProjectionOptions options,
        string partitionKey,
        string checkpointKey,
        long currentPosition,
        int eventsProcessedSinceCheckpoint,
        CancellationToken ct)
    {
        if (eventsProcessedSinceCheckpoint > 0)
        {
            try
            {
                await _checkpointStore.SaveCheckpointAsync(checkpointKey, currentPosition, ct);
                _monitor?.RecordCheckpointWritten(options.ProjectionName, partitionKey, currentPosition);

                _logger.LogInformation(
                    "Final checkpoint saved for {ProjectionName}:{PartitionKey} at position {Position}",
                    options.ProjectionName, partitionKey, currentPosition);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to save final checkpoint for {ProjectionName}:{PartitionKey} at position {Position}",
                    options.ProjectionName, partitionKey, currentPosition);
            }
        }
    }

    /// <summary>
    /// Processes a work item with error handling according to the projection's error handling strategy.
    /// </summary>
    /// <returns>True if processing should continue, False if the worker should stop.</returns>
    private async Task<bool> ProcessWorkItemWithErrorHandlingAsync(
        WorkItem workItem,
        ProjectionOptions options,
        string partitionKey,
        long currentPosition,
        CancellationToken ct)
    {
        var errorHandling = options.ErrorHandling;
        
        // Validate configuration before use
        errorHandling.Validate();
        
        // If strategy is not Retry, process once
        if (errorHandling.Strategy != ProjectionErrorHandlingStrategy.Retry)
        {
            try
            {
                await ProcessWorkItemAsync(workItem, ct);
                return true;
            }
            catch (Exception ex)
            {
                return await HandleProcessingErrorAsync(
                    ex,
                    workItem,
                    options,
                    partitionKey,
                    currentPosition,
                    errorHandling.Strategy,
                    1);
            }
        }
        
        // Retry strategy - attempt processing with exponential backoff
        var attempt = 0;
        var delay = errorHandling.InitialRetryDelayMs;
        
        while (attempt < errorHandling.MaxRetryAttempts)
        {
            try
            {
                await ProcessWorkItemAsync(workItem, ct);
                
                // Success - log retry success if this wasn't the first attempt
                if (attempt > 0)
                {
                    _logger.LogInformation(
                        "Successfully processed event for {ProjectionName}:{PartitionKey} at position {Position} after {Attempts} retry attempt(s)",
                        options.ProjectionName,
                        partitionKey,
                        currentPosition,
                        attempt);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                attempt++;
                
                // If we've exhausted retries, use fallback strategy
                if (attempt >= errorHandling.MaxRetryAttempts)
                {
                    _logger.LogError(
                        ex,
                        "Failed to process event for {ProjectionName}:{PartitionKey} at position {Position} after {MaxAttempts} attempts. Using fallback strategy: {FallbackStrategy}",
                        options.ProjectionName,
                        partitionKey,
                        currentPosition,
                        errorHandling.MaxRetryAttempts,
                        errorHandling.FallbackStrategy);
                    
                    return await HandleProcessingErrorAsync(
                        ex,
                        workItem,
                        options,
                        partitionKey,
                        currentPosition,
                        errorHandling.FallbackStrategy,
                        attempt);
                }
                
                // Log retry attempt with structured data
                _logger.LogWarning(
                    ex,
                    "Error processing event for {ProjectionName}:{PartitionKey} at position {Position}. Attempt {Attempt} of {MaxAttempts}. Retrying in {DelayMs}ms",
                    options.ProjectionName,
                    partitionKey,
                    currentPosition,
                    attempt,
                    errorHandling.MaxRetryAttempts,
                    delay);
                
                // Wait before retrying
                try
                {
                    await Task.Delay(delay, ct);
                }
                catch (OperationCanceledException)
                {
                    // Propagate cancellation instead of treating it as a retryable error
                    throw;
                }
                
                // Calculate next delay with exponential backoff
                delay = Math.Min(delay * 2, errorHandling.MaxRetryDelayMs);
            }
        }
        
        // Should never reach here, but return false to be safe
        return false;
    }

    /// <summary>
    /// Handles a processing error according to the specified strategy.
    /// </summary>
    /// <returns>True if processing should continue, False if the worker should stop.</returns>
    private async Task<bool> HandleProcessingErrorAsync(
        Exception ex,
        WorkItem workItem,
        ProjectionOptions options,
        string partitionKey,
        long currentPosition,
        ProjectionErrorHandlingStrategy strategy,
        int totalAttempts)
    {
        switch (strategy)
        {
            case ProjectionErrorHandlingStrategy.Skip:
                // Log structured error and continue
                _logger.LogError(
                    ex,
                    "Skipping failed event for {ProjectionName}:{PartitionKey} at position {Position} after {TotalAttempts} attempt(s). " +
                    "Event type: {EventType}, Handler: {HandlerType}. Error: {ErrorMessage}",
                    options.ProjectionName,
                    partitionKey,
                    currentPosition,
                    totalAttempts,
                    workItem.EventType.Name,
                    workItem.HandlerServiceType.Name,
                    ex.Message);
                
                return true; // Continue processing
                
            case ProjectionErrorHandlingStrategy.Stop:
                // Log structured error and stop
                _logger.LogCritical(
                    ex,
                    "Stopping projection worker for {ProjectionName}:{PartitionKey} at position {Position} after {TotalAttempts} attempt(s). " +
                    "Event type: {EventType}, Handler: {HandlerType}. Error: {ErrorMessage}",
                    options.ProjectionName,
                    partitionKey,
                    currentPosition,
                    totalAttempts,
                    workItem.EventType.Name,
                    workItem.HandlerServiceType.Name,
                    ex.Message);
                
                return false; // Stop worker
                
            case ProjectionErrorHandlingStrategy.Retry:
                // This should not happen as Retry is handled in the calling method
                _logger.LogError(
                    ex,
                    "Unexpected Retry strategy in error handler for {ProjectionName}:{PartitionKey} at position {Position}",
                    options.ProjectionName,
                    partitionKey,
                    currentPosition);
                return false;
                
            default:
                _logger.LogError(
                    ex,
                    "Unknown error handling strategy {Strategy} for {ProjectionName}:{PartitionKey} at position {Position}. Stopping worker.",
                    strategy,
                    options.ProjectionName,
                    partitionKey,
                    currentPosition);
                return false;
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
        else if (workItem.Cache.BatchHandlerInterface.IsAssignableFrom(workItem.HandlerServiceType))
        {
            // Single-event fallback for batch handlers (when BatchSize is 0 / not configured)
            var typedList = workItem.Cache.CreateTypedBatchList(new[] { workItem.Event });
            var projectTask = (ValueTask)workItem.Cache.ProjectBatchMethod.Invoke(handler, new[] { typedList, ct })!;
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
        public Type BatchHandlerInterface { get; }
        public MethodInfo RegularProjectMethod { get; }
        public MethodInfo PartitionedProjectMethod { get; }
        public MethodInfo? GetPartitionKeyMethod { get; }
        public MethodInfo ProjectBatchMethod { get; }
        
        private readonly Type _listType;
        private readonly MethodInfo _listAddMethod;

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
            BatchHandlerInterface = typeof(IProjectionBatchHandler<>).MakeGenericType(eventType);

            // Cache ProjectAsync methods
            RegularProjectMethod = RegularHandlerInterface.GetMethod(nameof(IProjectionHandler<object>.ProjectAsync))!;
            PartitionedProjectMethod = PartitionedHandlerInterface.GetMethod(nameof(IPartitionedProjectionHandler<object>.ProjectAsync))!;
            ProjectBatchMethod = BatchHandlerInterface.GetMethod(nameof(IProjectionBatchHandler<object>.ProjectBatchAsync))!;
            
            // Cache GetPartitionKey method for partitioned handlers
            GetPartitionKeyMethod = PartitionedHandlerInterface.GetMethod(nameof(IPartitionedProjectionHandler<object>.GetPartitionKey));
            
            // Cache List<TEvent> type for batch construction
            _listType = typeof(List<>).MakeGenericType(eventType);
            _listAddMethod = _listType.GetMethod("Add")!;
        }
        
        /// <summary>
        /// Creates a typed <c>List&lt;TEvent&gt;</c> from raw event objects (returned as <c>IReadOnlyList&lt;TEvent&gt;</c>).
        /// </summary>
        public object CreateTypedBatchList(IEnumerable<object> events)
        {
            var list = Activator.CreateInstance(_listType)!;
            foreach (var evt in events)
            {
                _listAddMethod.Invoke(list, new[] { evt });
            }
            return list;
        }
    }
}
