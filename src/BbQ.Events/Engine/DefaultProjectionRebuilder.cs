using BbQ.Events.Checkpointing;
using Microsoft.Extensions.Logging;

namespace BbQ.Events.Engine;

/// <summary>
/// Default implementation of the projection rebuilder.
/// 
/// This implementation provides checkpoint reset functionality for projections,
/// enabling rebuild scenarios. It works with the projection handler registry
/// and checkpoint store to manage projection state.
/// </summary>
internal class DefaultProjectionRebuilder : IProjectionRebuilder
{
    private readonly IProjectionCheckpointStore _checkpointStore;
    private readonly ILogger<DefaultProjectionRebuilder> _logger;

    /// <summary>
    /// Creates a new instance of the default projection rebuilder.
    /// </summary>
    /// <param name="checkpointStore">The checkpoint store for managing projection positions</param>
    /// <param name="logger">Logger for diagnostic output</param>
    public DefaultProjectionRebuilder(
        IProjectionCheckpointStore checkpointStore,
        ILogger<DefaultProjectionRebuilder> logger)
    {
        _checkpointStore = checkpointStore;
        _logger = logger;
    }

    /// <summary>
    /// Resets all registered projections, causing them to rebuild from the beginning.
    /// </summary>
    public async ValueTask ResetAllProjectionsAsync(CancellationToken ct = default)
    {
        var projectionNames = GetRegisteredProjections().ToList();

        if (projectionNames.Count == 0)
        {
            _logger.LogWarning("No projections registered. Nothing to reset.");
            return;
        }

        _logger.LogInformation("Resetting {Count} projection(s)", projectionNames.Count);

        var resetTasks = projectionNames.Select(projectionName => 
            ResetProjectionAsync(projectionName, ct).AsTask());

        await Task.WhenAll(resetTasks);

        _logger.LogInformation("Successfully reset all {Count} projection(s)", projectionNames.Count);
    }

    /// <summary>
    /// Resets a specific projection, causing it to rebuild from the beginning.
    /// </summary>
    public async ValueTask ResetProjectionAsync(string projectionName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(projectionName))
        {
            throw new ArgumentException(
                $"Projection name '{projectionName}' cannot be null, empty, or whitespace.",
                nameof(projectionName));
        }

        _logger.LogInformation("Resetting projection: {ProjectionName}", projectionName);

        // Reset the main projection checkpoint (for non-partitioned projections)
        await _checkpointStore.ResetCheckpointAsync(projectionName, ct);

        // Note: For partitioned projections, this resets the main checkpoint only.
        // Individual partition checkpoints are tracked with keys like "ProjectionName:PartitionKey"
        // and are created dynamically as events are processed. We cannot enumerate all
        // partitions without additional infrastructure (e.g., a partition registry or database query).
        // 
        // To reset individual partitions, callers should use ResetPartitionAsync with known
        // partition keys. This design provides flexibility - you can reset the main checkpoint
        // while preserving partition checkpoints if desired, or reset both by calling
        // ResetPartitionAsync for each partition separately.

        _logger.LogInformation("Successfully reset projection: {ProjectionName}", projectionName);
    }

    /// <summary>
    /// Resets a specific partition of a partitioned projection, causing it to rebuild from the beginning.
    /// </summary>
    public async ValueTask ResetPartitionAsync(string projectionName, string partitionKey, CancellationToken ct = default)
    {
        var projectionNameValue = projectionName ?? "<null>";
        if (string.IsNullOrWhiteSpace(projectionName))
        {
            throw new ArgumentException(
                $"Parameter '{nameof(projectionName)}' cannot be null or whitespace. Actual value: '{projectionNameValue}'.",
                nameof(projectionName));
        }

        var partitionKeyValue = partitionKey ?? "<null>";
        if (string.IsNullOrWhiteSpace(partitionKey))
        {
            throw new ArgumentException(
                $"Parameter '{nameof(partitionKey)}' cannot be null or whitespace. Actual value: '{partitionKeyValue}'.",
                nameof(partitionKey));
        }

        var checkpointKey = $"{projectionName}:{partitionKey}";

        _logger.LogInformation(
            "Resetting partition for projection: {ProjectionName}, partition: {PartitionKey}",
            projectionName,
            partitionKey);

        await _checkpointStore.ResetCheckpointAsync(checkpointKey, ct);

        _logger.LogInformation(
            "Successfully reset partition for projection: {ProjectionName}, partition: {PartitionKey}",
            projectionName,
            partitionKey);
    }

    /// <summary>
    /// Gets all registered projection names.
    /// </summary>
    public IEnumerable<string> GetRegisteredProjections()
    {
        // Get all unique projection names from the registry
        var eventTypes = ProjectionHandlerRegistry.GetEventTypes();
        var projectionNames = new HashSet<string>();

        foreach (var eventType in eventTypes)
        {
            var handlers = ProjectionHandlerRegistry.GetHandlers(eventType);
            foreach (var handlerType in handlers)
            {
                var registration = ProjectionHandlerRegistry.GetHandlerRegistration(eventType, handlerType);
                if (registration != null)
                {
                    projectionNames.Add(registration.ConcreteType.Name);
                }
            }
        }

        return projectionNames.OrderBy(name => name);
    }
}
