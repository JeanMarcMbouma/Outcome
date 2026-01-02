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
        if (!options.FromCheckpoint && !options.DryRun && options.CheckpointMode != CheckpointMode.None)
        {
            _logger.LogInformation(
                "Resetting checkpoint for {CheckpointKey} before replay",
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

            // Use reflection to call ReadAsync<TEvent> generically
            var readMethod = typeof(IEventStore).GetMethod(nameof(IEventStore.ReadAsync))!
                .MakeGenericMethod(eventType);

            var streamName = projectionName; // Use projection name as stream name
            var eventStream = readMethod.Invoke(_eventStore, new object[] { streamName, startPosition, cancellationToken });

            if (eventStream == null)
            {
                _logger.LogWarning("Event stream is null for event type {EventType}", eventType.Name);
                continue;
            }

            // Get the async enumerator
            var getEnumeratorMethod = eventStream.GetType().GetMethod("GetAsyncEnumerator");
            if (getEnumeratorMethod == null)
            {
                _logger.LogWarning("Could not get async enumerator for event type {EventType}", eventType.Name);
                continue;
            }

            var enumerator = getEnumeratorMethod.Invoke(eventStream, new object[] { cancellationToken });
            if (enumerator == null)
            {
                _logger.LogWarning("Enumerator is null for event type {EventType}", eventType.Name);
                continue;
            }

            var enumeratorType = enumerator.GetType();
            var moveNextMethod = enumeratorType.GetMethod("MoveNextAsync");
            var currentProperty = enumeratorType.GetProperty("Current");

            if (moveNextMethod == null || currentProperty == null)
            {
                _logger.LogWarning("Could not find MoveNextAsync or Current for event type {EventType}", eventType.Name);
                continue;
            }

            // Process events from the stream
            while (true)
            {
                var moveNextResult = moveNextMethod.Invoke(enumerator, Array.Empty<object>());
                if (moveNextResult == null) break;

                var moveNextTask = (ValueTask<bool>)moveNextResult;
                if (!await moveNextTask) break;

                var storedEvent = currentProperty.GetValue(enumerator);
                if (storedEvent == null) continue;

                // Extract position and event from StoredEvent<TEvent>
                var positionProp = storedEvent.GetType().GetProperty("Position");
                var eventProp = storedEvent.GetType().GetProperty("Event");

                if (positionProp == null || eventProp == null) continue;

                var position = (long)positionProp.GetValue(storedEvent)!;
                var @event = eventProp.GetValue(storedEvent);

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

                // Filter by partition if specified
                if (!string.IsNullOrEmpty(options.Partition))
                {
                    // Check if handler is partitioned and get partition key
                    var shouldSkipEvent = false;
                    foreach (var handlerType in handlers)
                    {
                        var partitionedInterface = handlerType.GetInterfaces()
                            .FirstOrDefault(i => i.IsGenericType &&
                                i.GetGenericTypeDefinition() == typeof(IPartitionedProjectionHandler<>));

                        if (partitionedInterface != null)
                        {
                            using var scope = _serviceProvider.CreateScope();
                            var handler = scope.ServiceProvider.GetRequiredService(handlerType);
                            var getPartitionKeyMethod = partitionedInterface.GetMethod("GetPartitionKey");
                            if (getPartitionKeyMethod != null)
                            {
                                var partitionKey = getPartitionKeyMethod.Invoke(handler, new[] { @event }) as string;
                                if (partitionKey != options.Partition)
                                {
                                    // Skip this event - wrong partition
                                    shouldSkipEvent = true;
                                    break;
                                }
                            }
                        }
                    }
                    
                    if (shouldSkipEvent)
                    {
                        continue;
                    }
                }

                // Process event through all handlers
                foreach (var handlerType in handlers)
                {
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var handler = scope.ServiceProvider.GetRequiredService(handlerType);

                        // Determine which ProjectAsync method to call
                        var projectMethod = handlerType.GetMethod("ProjectAsync");
                        if (projectMethod != null)
                        {
                            var projectTask = (ValueTask)projectMethod.Invoke(handler, new[] { @event, cancellationToken })!;
                            await projectTask;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Error processing event at position {Position} for projection {ProjectionName}, handler {HandlerType}",
                            position,
                            projectionName,
                            handlerType.Name);

                        // For replay, we continue processing even on errors (can be made configurable)
                        // The assumption is that replay is often used for recovery/testing
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
        }

        // Final checkpoint write (if needed)
        if (shouldWriteCheckpoints && (options.CheckpointMode == CheckpointMode.FinalOnly || 
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
}
