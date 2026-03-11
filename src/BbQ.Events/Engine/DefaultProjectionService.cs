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
/// Default implementation of the projection service with batch processing,
/// parallel processing, and automatic checkpointing.
/// 
/// This service collects events into configurable batches and dispatches them
/// to <see cref="IProjectionBatchHandler{TEvent}"/> handlers, providing efficient
/// bulk processing with automatic checkpoint management.
/// </summary>
/// <remarks>
/// This implementation:
/// - Subscribes to live event streams via the event bus
/// - Collects events into batches based on size and timeout
/// - Dispatches batches to registered batch handlers
/// - Supports parallel processing across partitions
/// - Automatically saves checkpoints after each successful batch
/// - Provides graceful shutdown with batch flushing and final checkpoint persistence
/// - Creates a DI scope per batch for handler resolution and processing
/// </remarks>
internal class DefaultProjectionService : IProjectionService
{
    private const string DefaultPartitionKey = "_default";

    private readonly IServiceProvider _serviceProvider;
    private readonly IEventBus _eventBus;
    private readonly IProjectionCheckpointStore _checkpointStore;
    private readonly IProjectionMonitor? _monitor;
    private readonly ILogger<DefaultProjectionService> _logger;

    // Cache reflection calls for performance
    private static readonly ConcurrentDictionary<Type, BatchReflectionCache> _reflectionCache = new();

    // Track partition workers: workerKey -> worker task/channel
    private readonly ConcurrentDictionary<string, BatchPartitionWorker> _partitionWorkers = new();

    // Semaphore for controlling parallelism across all partitions
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _projectionSemaphores = new();

    public DefaultProjectionService(
        IServiceProvider serviceProvider,
        IEventBus eventBus,
        IProjectionCheckpointStore checkpointStore,
        ILogger<DefaultProjectionService> logger,
        IProjectionMonitor? monitor = null)
    {
        _serviceProvider = serviceProvider;
        _eventBus = eventBus;
        _checkpointStore = checkpointStore;
        _monitor = monitor;
        _logger = logger;
    }

    /// <summary>
    /// Runs the projection service, processing events in batches until cancelled.
    /// </summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        var eventTypes = ProjectionHandlerRegistry.GetEventTypes()
            .Where(HasBatchHandlers)
            .ToList();

        if (eventTypes.Count == 0)
        {
            _logger.LogWarning("No batch projection handlers registered. Service will not process any events.");
            return;
        }

        _logger.LogInformation("Starting projection service with {HandlerCount} batch event type(s) registered",
            eventTypes.Count);

        var tasks = eventTypes
            .Select(eventType =>
            {
                var handlerServiceTypes = ProjectionHandlerRegistry.GetHandlers(eventType)
                    .Where(t => IsBatchHandler(t, eventType))
                    .ToList();
                return ProcessBatchEventStreamAsync(eventType, handlerServiceTypes, ct);
            })
            .ToList();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Projection service stopping gracefully...");
            await GracefulShutdownAsync();
            _logger.LogInformation("Projection service stopped gracefully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Projection service encountered an error");
            await GracefulShutdownAsync();
            throw;
        }
    }

    /// <summary>
    /// Checks whether a given event type has batch handler registrations.
    /// </summary>
    private static bool HasBatchHandlers(Type eventType)
    {
        var batchHandlerInterface = typeof(IProjectionBatchHandler<>).MakeGenericType(eventType);
        return ProjectionHandlerRegistry.GetHandlers(eventType)
            .Any(t => batchHandlerInterface.IsAssignableFrom(t));
    }

    /// <summary>
    /// Checks whether a handler service type is a batch handler for the given event type.
    /// </summary>
    private static bool IsBatchHandler(Type handlerServiceType, Type eventType)
    {
        var batchHandlerInterface = typeof(IProjectionBatchHandler<>).MakeGenericType(eventType);
        return batchHandlerInterface.IsAssignableFrom(handlerServiceType);
    }

    /// <summary>
    /// Gracefully shuts down all partition workers.
    /// </summary>
    private async Task GracefulShutdownAsync()
    {
        _logger.LogInformation("Shutting down {WorkerCount} batch partition worker(s)", _partitionWorkers.Count);

        foreach (var worker in _partitionWorkers.Values)
        {
            worker.Channel.Writer.Complete();
        }

        var workerTasks = _partitionWorkers.Values.Select(w => w.Task).ToList();
        await Task.WhenAll(workerTasks);

        _logger.LogInformation("All batch partition workers stopped");

        foreach (var semaphore in _projectionSemaphores.Values)
        {
            semaphore.Dispose();
        }
        _projectionSemaphores.Clear();
    }

    private async Task ProcessBatchEventStreamAsync(
        Type eventType,
        List<Type> handlerServiceTypes,
        CancellationToken ct)
    {
        _logger.LogInformation("Starting batch event stream processor for {EventType}", eventType.Name);

        try
        {
            var cache = _reflectionCache.GetOrAdd(eventType, type => new BatchReflectionCache(type));

            // Subscribe to the event bus
            var eventStream = cache.SubscribeMethod.Invoke(_eventBus, new object[] { ct });
            if (eventStream == null)
            {
                _logger.LogError("Subscribe returned null for event type {EventType}", eventType.Name);
                return;
            }

            var enumerator = cache.GetEnumeratorMethod.Invoke(eventStream, new object[] { ct });
            if (enumerator == null)
            {
                _logger.LogError("GetAsyncEnumerator returned null for event type {EventType}", eventType.Name);
                return;
            }

            var enumeratorType = enumerator.GetType();
            var moveNextMethod = GetMoveNextAsync(enumeratorType);
            var currentProperty = GetCurrentAsync(enumeratorType);

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
                    await DispatchEventToBatchWorkersAsync(eventType, currentEvent, handlerServiceTypes, cache, ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Batch event stream processor for {EventType} stopped", eventType.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing batch event stream for {EventType}", eventType.Name);
            throw;
        }
    }

    private async Task DispatchEventToBatchWorkersAsync(
        Type eventType,
        object @event,
        List<Type> handlerServiceTypes,
        BatchReflectionCache cache,
        CancellationToken ct)
    {
        foreach (var handlerServiceType in handlerServiceTypes)
        {
            try
            {
                var registration = ProjectionHandlerRegistry.GetHandlerRegistration(eventType, handlerServiceType);
                if (registration == null)
                {
                    _logger.LogWarning("No registration found for {HandlerType}", handlerServiceType.Name);
                    continue;
                }

                var options = GetServiceOptions(registration.ConcreteType);

                // Route to batch worker (default partition for batch handlers)
                var workerKey = $"batch:{options.ProjectionName}:{DefaultPartitionKey}";

                var worker = _partitionWorkers.GetOrAdd(workerKey, _ =>
                {
                    _logger.LogInformation(
                        "Creating batch partition worker for projection {ProjectionName}",
                        options.ProjectionName);

                    var semaphore = _projectionSemaphores.GetOrAdd(
                        options.ProjectionName,
                        _ =>
                        {
                            var maxCount = options.MaxDegreeOfParallelism > 0
                                ? options.MaxDegreeOfParallelism
                                : 1000;
                            return new SemaphoreSlim(maxCount, maxCount);
                        });

                    var channel = Channel.CreateBounded<BatchWorkItem>(
                        new BoundedChannelOptions(options.ChannelCapacity)
                        {
                            FullMode = BoundedChannelFullMode.Wait,
                            SingleReader = true,
                            SingleWriter = false
                        });

                    var task = Task.Run(
                        async () => await ProcessBatchPartitionAsync(
                            handlerServiceType,
                            eventType,
                            options,
                            channel,
                            semaphore,
                            cache,
                            CancellationToken.None),
                        CancellationToken.None);

                    return new BatchPartitionWorker
                    {
                        Channel = channel,
                        Task = task
                    };
                });

                var workItem = new BatchWorkItem { Event = @event };
                await worker.Channel.Writer.WriteAsync(workItem, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error routing {EventType} to batch handler {HandlerType}",
                    eventType.Name, handlerServiceType.Name);
            }
        }
    }

    /// <summary>
    /// Processes events for a batch partition, collecting events into batches and dispatching.
    /// </summary>
    private async Task ProcessBatchPartitionAsync(
        Type handlerServiceType,
        Type eventType,
        ProjectionServiceOptions options,
        Channel<BatchWorkItem> channel,
        SemaphoreSlim semaphore,
        BatchReflectionCache cache,
        CancellationToken ct)
    {
        var checkpointKey = $"{options.ProjectionName}:{DefaultPartitionKey}";

        try
        {
            // Determine starting position
            long? checkpoint = null;
            if (options.StartupMode == ProjectionStartupMode.Resume)
            {
                checkpoint = await _checkpointStore.GetCheckpointAsync(checkpointKey, ct);
                _logger.LogInformation(
                    "Batch worker for {ProjectionName} resuming from checkpoint {Checkpoint}",
                    options.ProjectionName,
                    checkpoint.HasValue ? checkpoint.Value.ToString() : "beginning");
            }
            else if (options.StartupMode == ProjectionStartupMode.Replay)
            {
                await _checkpointStore.ResetCheckpointAsync(checkpointKey, ct);
                _logger.LogInformation(
                    "Batch worker for {ProjectionName} replaying from beginning",
                    options.ProjectionName);
            }

            var currentPosition = checkpoint ?? 0;
            var batch = new List<object>(options.BatchSize);
            var batchTimer = new System.Diagnostics.Stopwatch();

            _logger.LogInformation(
                "Batch partition worker started for {ProjectionName} (BatchSize={BatchSize}, BatchTimeout={BatchTimeout}ms, MaxParallelism={MaxParallelism}, AutoCheckpoint={AutoCheckpoint})",
                options.ProjectionName,
                options.BatchSize,
                options.BatchTimeout.TotalMilliseconds,
                options.MaxDegreeOfParallelism,
                options.AutoCheckpoint);

            batchTimer.Start();

            await foreach (var workItem in channel.Reader.ReadAllAsync(ct))
            {
                batch.Add(workItem.Event);

                // Check if batch is full or timeout expired
                var batchFull = batch.Count >= options.BatchSize;
                var timeoutExpired = batchTimer.Elapsed >= options.BatchTimeout;

                if (batchFull || timeoutExpired)
                {
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        var batchToProcess = new List<object>(batch);
                        batch.Clear();
                        batchTimer.Restart();

                        var shouldContinue = await ProcessBatchWithErrorHandlingAsync(
                            handlerServiceType,
                            eventType,
                            batchToProcess,
                            options,
                            currentPosition,
                            cache,
                            ct);

                        if (!shouldContinue)
                        {
                            _logger.LogWarning(
                                "Stopping batch worker for {ProjectionName} due to error handling policy",
                                options.ProjectionName);
                            return;
                        }

                        currentPosition += batchToProcess.Count;

                        // Record metrics
                        _monitor?.RecordEventProcessed(options.ProjectionName, DefaultPartitionKey, currentPosition);
                        _monitor?.RecordQueueDepth(options.ProjectionName, DefaultPartitionKey, channel.Reader.Count);

                        // Automatic checkpointing
                        if (options.AutoCheckpoint)
                        {
                            await _checkpointStore.SaveCheckpointAsync(checkpointKey, currentPosition, ct);
                            _monitor?.RecordCheckpointWritten(options.ProjectionName, DefaultPartitionKey, currentPosition);

                            _logger.LogDebug(
                                "Checkpoint saved for {ProjectionName} at position {Position} after batch of {BatchCount} events",
                                options.ProjectionName,
                                currentPosition,
                                batchToProcess.Count);
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }
            }

            // Process remaining events in the batch (flush on shutdown)
            if (batch.Count > 0)
            {
                _logger.LogInformation(
                    "Flushing final batch of {BatchCount} events for {ProjectionName}",
                    batch.Count,
                    options.ProjectionName);

                await semaphore.WaitAsync(ct);
                try
                {
                    var shouldContinue = await ProcessBatchWithErrorHandlingAsync(
                        handlerServiceType,
                        eventType,
                        batch,
                        options,
                        currentPosition,
                        cache,
                        ct);

                    if (shouldContinue)
                    {
                        currentPosition += batch.Count;

                        if (options.AutoCheckpoint)
                        {
                            try
                            {
                                await _checkpointStore.SaveCheckpointAsync(checkpointKey, currentPosition, ct);
                                _monitor?.RecordCheckpointWritten(options.ProjectionName, DefaultPartitionKey, currentPosition);

                                _logger.LogInformation(
                                    "Final checkpoint saved for {ProjectionName} at position {Position}",
                                    options.ProjectionName,
                                    currentPosition);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(
                                    ex,
                                    "Failed to save final checkpoint for {ProjectionName} at position {Position}",
                                    options.ProjectionName,
                                    currentPosition);
                            }
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }

            _logger.LogInformation(
                "Batch partition worker stopped for {ProjectionName}",
                options.ProjectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Fatal error in batch partition worker for {ProjectionName}",
                options.ProjectionName);
            throw;
        }
    }

    /// <summary>
    /// Processes a batch with error handling according to the projection's error handling strategy.
    /// </summary>
    /// <returns>True if processing should continue, False if the worker should stop.</returns>
    private async Task<bool> ProcessBatchWithErrorHandlingAsync(
        Type handlerServiceType,
        Type eventType,
        List<object> batch,
        ProjectionServiceOptions options,
        long currentPosition,
        BatchReflectionCache cache,
        CancellationToken ct)
    {
        var errorHandling = options.ErrorHandling;
        errorHandling.Validate();

        if (errorHandling.Strategy != ProjectionErrorHandlingStrategy.Retry)
        {
            try
            {
                await InvokeBatchHandlerAsync(handlerServiceType, eventType, batch, cache, ct);
                return true;
            }
            catch (Exception ex)
            {
                return HandleBatchError(ex, options, currentPosition, batch.Count, errorHandling.Strategy, 1);
            }
        }

        // Retry strategy
        var attempt = 0;
        var delay = errorHandling.InitialRetryDelayMs;

        while (attempt < errorHandling.MaxRetryAttempts)
        {
            try
            {
                await InvokeBatchHandlerAsync(handlerServiceType, eventType, batch, cache, ct);

                if (attempt > 0)
                {
                    _logger.LogInformation(
                        "Successfully processed batch for {ProjectionName} at position {Position} after {Attempts} retry attempt(s)",
                        options.ProjectionName,
                        currentPosition,
                        attempt);
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
                        "Failed to process batch for {ProjectionName} at position {Position} after {MaxAttempts} attempts. Using fallback strategy: {FallbackStrategy}",
                        options.ProjectionName,
                        currentPosition,
                        errorHandling.MaxRetryAttempts,
                        errorHandling.FallbackStrategy);

                    return HandleBatchError(ex, options, currentPosition, batch.Count, errorHandling.FallbackStrategy, attempt);
                }

                _logger.LogWarning(
                    ex,
                    "Error processing batch for {ProjectionName} at position {Position}. Attempt {Attempt} of {MaxAttempts}. Retrying in {DelayMs}ms",
                    options.ProjectionName,
                    currentPosition,
                    attempt,
                    errorHandling.MaxRetryAttempts,
                    delay);

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
        ProjectionServiceOptions options,
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
                    "Skipping failed batch of {BatchCount} events for {ProjectionName} at position {Position} after {TotalAttempts} attempt(s). Error: {ErrorMessage}",
                    batchCount,
                    options.ProjectionName,
                    currentPosition,
                    totalAttempts,
                    ex.Message);
                return true;

            case ProjectionErrorHandlingStrategy.Stop:
                _logger.LogCritical(
                    ex,
                    "Stopping projection service for {ProjectionName} at position {Position} after {TotalAttempts} attempt(s). Batch size: {BatchCount}. Error: {ErrorMessage}",
                    options.ProjectionName,
                    currentPosition,
                    totalAttempts,
                    batchCount,
                    ex.Message);
                return false;

            default:
                _logger.LogError(
                    ex,
                    "Unknown error handling strategy {Strategy} for {ProjectionName}. Stopping service.",
                    strategy,
                    options.ProjectionName);
                return false;
        }
    }

    /// <summary>
    /// Invokes the batch handler using reflection to call ProjectBatchAsync with a typed list.
    /// </summary>
    private async Task InvokeBatchHandlerAsync(
        Type handlerServiceType,
        Type eventType,
        List<object> batch,
        BatchReflectionCache cache,
        CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService(handlerServiceType);

        // Create a typed list from the batch
        var typedList = cache.CreateTypedList(batch);

        // Invoke ProjectBatchAsync
        var projectTask = (ValueTask)cache.ProjectBatchMethod.Invoke(handler, new[] { typedList, ct })!;
        await projectTask;
    }

    /// <summary>
    /// Gets projection service options for a handler type.
    /// </summary>
    private static ProjectionServiceOptions GetServiceOptions(Type concreteType)
    {
        var registeredOptions = ProjectionHandlerRegistry.GetProjectionServiceOptions(concreteType.Name);
        if (registeredOptions != null)
        {
            return registeredOptions;
        }

        var attribute = concreteType.GetCustomAttribute<Projections.ProjectionAttribute>();

        return new ProjectionServiceOptions
        {
            BatchSize = attribute?.CheckpointBatchSize ?? 100,
            MaxDegreeOfParallelism = attribute?.MaxDegreeOfParallelism ?? 1,
            StartupMode = attribute?.StartupMode ?? ProjectionStartupMode.Resume,
            ChannelCapacity = attribute?.ChannelCapacity ?? 1000,
            ProjectionName = ProjectionNameResolver.Resolve(concreteType,
                new ProjectionOptions { ProjectionName = concreteType.Name })
        };
    }

    #region Reflection Helpers

    private static MethodInfo? GetMoveNextAsync(Type? type)
    {
        if (type == null) return null;

        var method = type.GetMethod("MoveNextAsync", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method != null) return method;

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

        return null;
    }

    private static PropertyInfo? GetCurrentAsync(Type? type)
    {
        if (type == null) return null;

        var property = type.GetProperty("Current", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null) return property;

        foreach (var iface in type.GetInterfaces())
        {
            var map = type.GetInterfaceMap(iface);
            for (int i = 0; i < map.InterfaceMethods.Length; i++)
            {
                if (map.InterfaceMethods[i].Name == "get_Current")
                {
                    var getter = map.TargetMethods[i];
                    var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var p in props)
                    {
                        if (p.GetMethod == getter) return p;
                    }
                    return null;
                }
            }
        }

        return null;
    }

    #endregion

    #region Inner Types

    private class BatchWorkItem
    {
        public object Event { get; set; } = null!;
    }

    private class BatchPartitionWorker
    {
        public Channel<BatchWorkItem> Channel { get; set; } = null!;
        public Task Task { get; set; } = null!;
    }

    /// <summary>
    /// Caches reflection information for batch handler operations.
    /// </summary>
    private class BatchReflectionCache
    {
        public MethodInfo SubscribeMethod { get; }
        public MethodInfo GetEnumeratorMethod { get; }
        public MethodInfo ProjectBatchMethod { get; }
        public Type BatchHandlerInterface { get; }

        private readonly Type _eventType;
        private readonly Type _listType;
        private readonly Type _readOnlyListType;

        public BatchReflectionCache(Type eventType)
        {
            _eventType = eventType;

            // Cache Subscribe<TEvent> method
            SubscribeMethod = typeof(IEventBus)
                .GetMethod(nameof(IEventBus.Subscribe))!
                .MakeGenericMethod(eventType);

            // Cache GetAsyncEnumerator method
            var asyncEnumerableType = typeof(IAsyncEnumerable<>).MakeGenericType(eventType);
            GetEnumeratorMethod = asyncEnumerableType.GetMethod("GetAsyncEnumerator")!;

            // Cache batch handler interface and method
            BatchHandlerInterface = typeof(IProjectionBatchHandler<>).MakeGenericType(eventType);
            ProjectBatchMethod = BatchHandlerInterface.GetMethod(nameof(IProjectionBatchHandler<object>.ProjectBatchAsync))!;

            // Cache list types
            _listType = typeof(List<>).MakeGenericType(eventType);
            _readOnlyListType = typeof(IReadOnlyList<>).MakeGenericType(eventType);
        }

        /// <summary>
        /// Creates a typed List&lt;TEvent&gt; from a list of objects.
        /// </summary>
        public object CreateTypedList(List<object> items)
        {
            var typedList = Activator.CreateInstance(_listType, items.Count)!;
            var addMethod = _listType.GetMethod("Add")!;
            foreach (var item in items)
            {
                addMethod.Invoke(typedList, new[] { item });
            }
            return typedList;
        }
    }

    #endregion
}
