using BbQ.Events.Checkpointing;
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
/// - Provides logging and progress tracking
/// 
/// The replay service keeps the projection engine focused on event processing
/// while handling replay-specific orchestration externally.
/// 
/// Note: This is a foundational implementation that manages replay configuration
/// and checkpoint state. Full event streaming replay will be implemented when
/// event store integration is available.
/// </remarks>
internal class DefaultReplayService : IReplayService
{
    private readonly IProjectionCheckpointStore _checkpointStore;
    private readonly ILogger<DefaultReplayService> _logger;

    public DefaultReplayService(
        IProjectionCheckpointStore checkpointStore,
        ILogger<DefaultReplayService> logger)
    {
        _checkpointStore = checkpointStore;
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

        // TODO: When event store integration is available, implement event streaming here:
        // 1. Query event store for events in range [startPosition, ToPosition]
        // 2. Create a replay-specific event stream
        // 3. Feed events to projection handlers
        // 4. Handle checkpointing according to CheckpointMode
        // 5. Track and report progress
        // 
        // For now, this validates configuration and manages checkpoint state.
        // The actual event replay will be driven by the projection engine when
        // it's restarted with StartupMode.Replay or through event store queries.

        _logger.LogInformation(
            "Replay configuration validated for {CheckpointKey}. " +
            "Note: Full event streaming replay will be available when event store integration is implemented. " +
            "For now, restart the projection engine with StartupMode.Replay to replay from checkpoints.",
            checkpointKey);

        _logger.LogInformation(
            "Replay preparation completed for {CheckpointKey}",
            checkpointKey);
    }
}
