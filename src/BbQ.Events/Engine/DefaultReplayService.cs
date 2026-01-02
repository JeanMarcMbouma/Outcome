using BbQ.Events.Checkpointing;
using BbQ.Events.Events;
using BbQ.Events.Projections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BbQ.Events.Engine;

/// <summary>
/// Default implementation of the replay service.
/// </summary>
/// <remarks>
/// This coordinator is responsible for orchestrating replay operations:
/// - Validates replay configuration
/// - Resolves projection metadata
/// - Manages checkpoint state during replay
/// - Streams events from IEventStore
/// - Invokes projection handlers with error handling
/// - Provides logging and progress tracking
/// 
/// The replay service keeps the projection engine focused on event processing
/// while handling replay-specific orchestration externally.
/// 
/// <b>Important Notes:</b>
/// 
/// <b>Stream Naming:</b> Events are read from a stream named after the projection.
/// Ensure events for a projection are appended to a stream matching the projection name.
/// 
/// <b>Checkpoint Tracking:</b> When a projection handles multiple event types, all events
/// are expected to be in the same stream with monotonically increasing positions.
/// If event types are in separate streams with independent positions, checkpoint tracking
/// may not work correctly for interrupted replays.
/// 
/// <b>Error Handling:</b> By default, errors during event processing are logged and replay
/// continues. This ensures replay can complete even if some events fail. Consider this
/// behavior when replaying critical projections.
/// </remarks>
internal class DefaultReplayService : IReplayService
{
    private readonly IProjectionCheckpointStore _checkpointStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventStore? _eventStore;
    private readonly ILogger<DefaultReplayService> _logger;

    public DefaultReplayService(
        IProjectionCheckpointStore checkpointStore,
        IServiceProvider serviceProvider,
        ILogger<DefaultReplayService> logger,
        IEventStore? eventStore = null)
    {
        _checkpointStore = checkpointStore;
        _serviceProvider = serviceProvider;
        _eventStore = eventStore;
        _logger = logger;
    }

    /// <summary>
    /// Replays a projection from historical events.
    /// </summary>
    public async Task ReplayAsync(
        string projectionName,
        ReplayOptions options,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(projectionName))
        {
            throw new ArgumentException(
                "Projection name cannot be null or empty.",
                nameof(projectionName));
        }

        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        // Validate replay options
        options.Validate();

        _logger.LogInformation(
            "Starting replay for projection {ProjectionName} with options: FromPosition={FromPosition}, ToPosition={ToPosition}, " +
            "FromCheckpoint={FromCheckpoint}, Partition={Partition}, BatchSize={BatchSize}, DryRun={DryRun}, CheckpointMode={CheckpointMode}",
            projectionName,
            options.FromPosition,
            options.ToPosition,
            options.FromCheckpoint,
            options.Partition ?? "(all)",
            options.BatchSize,
            options.DryRun,
            options.CheckpointMode);

        // Verify projection exists
        var eventTypes = ProjectionHandlerRegistry.GetEventTypes().ToList();
        var registeredProjections = new HashSet<string>();

        foreach (var eventType in eventTypes)
        {
            var handlers = ProjectionHandlerRegistry.GetHandlers(eventType);
            foreach (var handlerType in handlers)
            {
                var registration = ProjectionHandlerRegistry.GetHandlerRegistration(eventType, handlerType);
                if (registration != null)
                {
                    registeredProjections.Add(registration.ConcreteType.Name);
                }
            }
        }

        if (!registeredProjections.Contains(projectionName))
        {
            throw new InvalidOperationException(
                $"Projection '{projectionName}' is not registered. " +
                $"Registered projections: {string.Join(", ", registeredProjections.OrderBy(p => p))}");
        }

        // Build checkpoint key (includes partition if specified)
        var checkpointKey = string.IsNullOrEmpty(options.Partition)
            ? projectionName
            : $"{projectionName}:{options.Partition}";

        // Determine starting position
        long startPosition;
        if (options.FromPosition.HasValue)
        {
            // Explicit FromPosition takes precedence
            startPosition = options.FromPosition.Value;
            _logger.LogInformation(
                "Replay will start from explicit position {Position} for {CheckpointKey}",
                startPosition,
                checkpointKey);
        }
        else if (options.FromCheckpoint)
        {
            // Resume from checkpoint
            var checkpoint = await _checkpointStore.GetCheckpointAsync(checkpointKey, cancellationToken);
            startPosition = checkpoint ?? 0;
            _logger.LogInformation(
                "Replay will resume from checkpoint position {Position} for {CheckpointKey}",
                startPosition,
                checkpointKey);
        }
        else
        {
            // Start from beginning
            startPosition = 0;
            _logger.LogInformation(
                "Replay will start from beginning (position 0) for {CheckpointKey}",
                checkpointKey);
        }

        // Reset checkpoint if not resuming from checkpoint and not in dry run
        // Note: This resets the checkpoint even if FromPosition is specified.
        // If replay is interrupted, resuming requires specifying FromCheckpoint=true
        // or providing FromPosition again, as the checkpoint will be null.
        if (!options.FromCheckpoint && !options.DryRun && options.CheckpointMode != CheckpointMode.None)
        {
            _logger.LogInformation(
                "Resetting checkpoint for {CheckpointKey} before replay. " +
                "To resume interrupted replay, use FromCheckpoint=true or specify FromPosition again.",
                checkpointKey);
            await _checkpointStore.ResetCheckpointAsync(checkpointKey, cancellationToken);
        }

        // Log replay plan
        var endPositionDescription = options.ToPosition.HasValue
            ? $"position {options.ToPosition.Value}"
            : "end of stream";

        _logger.LogInformation(
            "Replay plan for {CheckpointKey}: Process events from position {StartPosition} to {EndPosition}",
            checkpointKey,
            startPosition,
            endPositionDescription);

        if (options.DryRun)
        {
            _logger.LogWarning(
                "Replay is running in DRY RUN mode for {CheckpointKey}. No checkpoints will be written.",
                checkpointKey);
        }

        if (options.CheckpointMode == CheckpointMode.None)
        {
            _logger.LogInformation(
                "CheckpointMode is None for {CheckpointKey}. No checkpoints will be written.",
                checkpointKey);
        }
        else if (options.CheckpointMode == CheckpointMode.FinalOnly)
        {
            _logger.LogInformation(
                "CheckpointMode is FinalOnly for {CheckpointKey}. Checkpoint will be written only after replay completes.",
                checkpointKey);
        }

        // Check if event store is available for streaming
        if (_eventStore == null)
        {
            _logger.LogWarning(
                "IEventStore not registered. Replay configuration validated for {CheckpointKey}, but event streaming is not available. " +
                "Register an IEventStore implementation (e.g., InMemoryEventStore or SqlServerEventStore) to enable event streaming. " +
                "For now, restart the projection engine with StartupMode.Replay to replay from checkpoints.",
                checkpointKey);

            _logger.LogInformation(
                "Replay preparation completed for {CheckpointKey}",
                checkpointKey);
            return;
        }

        // Stream events and process through projection handlers
        await StreamEventsAndProcessAsync(
            projectionName,
            checkpointKey,
            startPosition,
            options,
            cancellationToken);

        _logger.LogInformation(
            "Replay completed for {CheckpointKey}",
            checkpointKey);
    }

    /// <summary>
    /// Streams events from the event store and processes them through projection handlers.
    /// </summary>
    private async Task StreamEventsAndProcessAsync(
        string projectionName,
        string checkpointKey,
        long startPosition,
        ReplayOptions options,
        CancellationToken cancellationToken)
    {
        // Get event types and handlers for this projection
        var eventTypes = ProjectionHandlerRegistry.GetEventTypes().ToList();
        var projectionHandlers = new Dictionary<Type, List<Type>>();

        foreach (var eventType in eventTypes)
        {
            var handlers = ProjectionHandlerRegistry.GetHandlers(eventType);
            foreach (var handlerType in handlers)
            {
                var registration = ProjectionHandlerRegistry.GetHandlerRegistration(eventType, handlerType);
                if (registration != null && registration.ConcreteType.Name == projectionName)
                {
                    if (!projectionHandlers.ContainsKey(eventType))
                    {
                        projectionHandlers[eventType] = new List<Type>();
                    }
                    projectionHandlers[eventType].Add(handlerType);
                }
            }
        }

        if (projectionHandlers.Count == 0)
        {
            _logger.LogWarning(
                "No event handlers found for projection {ProjectionName}",
                projectionName);
            return;
        }

        _logger.LogInformation(
            "Found {HandlerCount} event type(s) handled by projection {ProjectionName}",
            projectionHandlers.Count,
            projectionName);

        // Process events for each event type
        long eventsProcessed = 0;
        long currentPosition = startPosition;
        var batchSize = options.BatchSize ?? 100;
        var shouldWriteCheckpoints = !options.DryRun && options.CheckpointMode != CheckpointMode.None;
        var normalCheckpointing = shouldWriteCheckpoints && options.CheckpointMode == CheckpointMode.Normal;

        foreach (var (eventType, handlers) in projectionHandlers)
        {
            _logger.LogInformation(
                "Processing {EventType} events for projection {ProjectionName}",
                eventType.Name,
                projectionName);

            // Use a generic helper method via reflection for type-safe event streaming
            var methodInfo = typeof(DefaultReplayService)
                .GetMethod(nameof(ProcessTypedEventStreamAsync), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (methodInfo is null)
            {
                _logger.LogError(
                    "Could not find method '{MethodName}' on '{TypeName}'",
                    nameof(ProcessTypedEventStreamAsync),
                    nameof(DefaultReplayService));
                continue;
            }

            var processMethod = methodInfo.MakeGenericMethod(eventType);

            var streamName = projectionName; // Use projection name as stream name
            
            var invocationResult = processMethod.Invoke(this, new object[] 
            { 
                streamName,
                handlers, 
                options, 
                checkpointKey, 
                eventsProcessed, 
                startPosition,  // Use startPosition for reading, not currentPosition
                batchSize, 
                normalCheckpointing, 
                cancellationToken 
            });

            if (invocationResult is not Task<(long, long)> task)
            {
                _logger.LogError(
                    "Method '{MethodName}' did not return expected type Task<(long, long)>",
                    nameof(ProcessTypedEventStreamAsync));
                continue;
            }
            
            var result = await task;
            eventsProcessed = result.Item1;
            currentPosition = result.Item2;
        }

        // Final checkpoint write (if needed)
        // Only write if: we should write checkpoints AND we actually processed events AND
        // (it's FinalOnly mode OR we have unwritten events in Normal mode)
        if (shouldWriteCheckpoints && eventsProcessed > 0 && 
            (options.CheckpointMode == CheckpointMode.FinalOnly || 
            (normalCheckpointing && eventsProcessed % batchSize != 0)))
        {
            await _checkpointStore.SaveCheckpointAsync(checkpointKey, currentPosition, cancellationToken);
            _logger.LogInformation(
                "Final checkpoint saved for {CheckpointKey} at position {Position}",
                checkpointKey,
                currentPosition);
        }

        _logger.LogInformation(
            "Replay processed {EventsProcessed} event(s) for {CheckpointKey}",
            eventsProcessed,
            checkpointKey);
    }

    /// <summary>
    /// Processes events from a stream for a specific event type.
    /// </summary>
    private async Task<(long, long)> ProcessTypedEventStreamAsync<TEvent>(
        string streamName,
        List<Type> handlers,
        ReplayOptions options,
        string checkpointKey,
        long eventsProcessed,
        long startPosition,
        int batchSize,
        bool normalCheckpointing,
        CancellationToken cancellationToken)
    {
        // Cache GetMethod results for handlers to avoid repeated reflection calls
        var handlerMethods = new Dictionary<Type, System.Reflection.MethodInfo?>();
        foreach (var handlerType in handlers)
        {
            handlerMethods[handlerType] = handlerType.GetMethod("ProjectAsync");
        }

        // For partition filtering: determine partition key per event inline (stateless operation)
        Type? partitionedHandlerType = null;
        System.Reflection.MethodInfo? getPartitionKeyMethod = null;
        
        if (!string.IsNullOrEmpty(options.Partition))
        {
            foreach (var handlerType in handlers)
            {
                var partitionedInterface = handlerType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType &&
                        i.GetGenericTypeDefinition() == typeof(IPartitionedProjectionHandler<>));

                if (partitionedInterface != null)
                {
                    partitionedHandlerType = handlerType;
                    getPartitionKeyMethod = partitionedInterface.GetMethod("GetPartitionKey");
                    break;
                }
            }
        }

        long currentPosition = startPosition;
        
        // Read events from the event store starting from the specified position
        await foreach (var storedEvent in _eventStore!.ReadAsync<TEvent>(streamName, startPosition, cancellationToken))
        {
            var position = storedEvent.Position;
            var @event = storedEvent.Event;

            if (@event == null) continue;

            // Check if we've exceeded ToPosition
            if (options.ToPosition.HasValue && position > options.ToPosition.Value)
            {
                _logger.LogInformation(
                    "Reached ToPosition {ToPosition} for {CheckpointKey}",
                    options.ToPosition.Value,
                    checkpointKey);
                break;
            }

            // Filter by partition if specified (create handler per event for stateless partition key check)
            if (!string.IsNullOrEmpty(options.Partition) && getPartitionKeyMethod != null && partitionedHandlerType != null)
            {
                using var partitionScope = _serviceProvider.CreateScope();
                var partitionHandler = partitionScope.ServiceProvider.GetRequiredService(partitionedHandlerType);
                var partitionKeyResult = getPartitionKeyMethod.Invoke(partitionHandler, new[] { (object)@event });
                if (partitionKeyResult is string partitionKey && partitionKey != options.Partition)
                {
                    continue; // Skip this event - wrong partition
                }
            }

            // Process event through all handlers
            foreach (var handlerType in handlers)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var handler = scope.ServiceProvider.GetRequiredService(handlerType);

                    // Use cached ProjectAsync method
                    if (handlerMethods.TryGetValue(handlerType, out var projectMethod) && projectMethod != null)
                    {
                        try
                        {
                            var result = projectMethod.Invoke(handler, new object[] { @event, cancellationToken });
                            if (result is ValueTask projectTask)
                            {
                                await projectTask;
                            }
                        }
                        catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException != null)
                        {
                            // Unwrap TargetInvocationException to get the actual exception
                            throw tie.InnerException;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log error and continue processing
                    // This behavior allows replay to continue even if some events fail
                    // Consider making this configurable in future versions
                    _logger.LogError(
                        ex,
                        "Error processing event at position {Position} for {CheckpointKey}, handler {HandlerType}. Continuing replay.",
                        position,
                        checkpointKey,
                        handlerType.Name);
                }
            }

            eventsProcessed++;
            currentPosition = position;

            // Checkpoint if needed (normal mode with batch size reached)
            if (normalCheckpointing && eventsProcessed % batchSize == 0)
            {
                await _checkpointStore.SaveCheckpointAsync(checkpointKey, currentPosition, cancellationToken);
                _logger.LogDebug(
                    "Checkpoint saved for {CheckpointKey} at position {Position} ({EventsProcessed} events processed)",
                    checkpointKey,
                    currentPosition,
                    eventsProcessed);
            }
        }
        
        return (eventsProcessed, currentPosition);
    }
}
