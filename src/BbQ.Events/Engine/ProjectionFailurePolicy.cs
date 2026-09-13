using BbQ.Events.Serialization;

namespace BbQ.Events.Engine;

/// <summary>An event in a failed handler invocation. EventId must be stable across redelivery for quarantine.</summary>
public sealed record ProjectionFailureEvent(string? EventId, object Value, Type EventType);

/// <summary>Failure context. Position is the partition worker's next local checkpoint ordinal, not a broker offset.</summary>
public sealed record ProjectionFailureContext(string ProjectionName, string PartitionKey, long Position,
    IReadOnlyList<ProjectionFailureEvent> Events, Exception Exception, int Attempt);

/// <summary>Replaceable classification policy. Cancellation and policy exceptions stop processing without acknowledging.</summary>
public interface IProjectionFailurePolicy
{
    ValueTask<ProjectionErrorHandlingStrategy> DecideAsync(ProjectionFailureContext context, CancellationToken ct = default);
}

/// <summary>Preserves the existing total-attempt cap and configured fallback.</summary>
public sealed class DefaultProjectionFailurePolicy(ProjectionErrorHandlingOptions options) : IProjectionFailurePolicy
{
    public ValueTask<ProjectionErrorHandlingStrategy> DecideAsync(ProjectionFailureContext context, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(options.Strategy == ProjectionErrorHandlingStrategy.Retry && context.Attempt >= options.MaxRetryAttempts
            ? options.FallbackStrategy : options.Strategy);
    }
}

/// <summary>Separate terminal dispositions: skipping and quarantining are not successful projection.</summary>
public enum ProjectionDisposition { Projected, Skipped, Quarantined, Stopped }

/// <summary>Optional monitoring extension for failure dispositions.</summary>
public interface IProjectionDispositionMonitor
{
    void RecordDisposition(string projectionName, string partitionKey, long position, ProjectionDisposition disposition);
}

/// <summary>Durable quarantine entry. ErrorCode contains an exception type only; messages/stacks are never persisted.</summary>
public sealed record ProjectionDeadLetter(string Id, string ProjectionName, string PartitionKey, string EventId,
    long Position, SerializedEvent Event, string ErrorCode, int Attempts, DateTimeOffset CreatedAt);

/// <summary>
/// Optional durable persistence. Put must be idempotent by Id and return only after durable commit.
/// A conflicting identity/payload must fail. Get is used to recover persistence-before-checkpoint crashes.
/// Implementations must retain entries across replay and must not imply cross-store atomicity.
/// </summary>
public interface IProjectionDeadLetterStore
{
    Task PutAsync(ProjectionDeadLetter entry, CancellationToken ct = default);
    Task<ProjectionDeadLetter?> GetAsync(string id, CancellationToken ct = default);
    IAsyncEnumerable<ProjectionDeadLetter> ReadAsync(CancellationToken ct = default);
}

/// <summary>Executes the existing retry/skip/stop loop with optional classification and quarantine persistence.</summary>
public sealed class ProjectionFailureProcessor
{
    public static string GetDeadLetterId(string projection, string partition, string eventId)
    {
        var value = $"{projection.Length}:{projection}{partition.Length}:{partition}{eventId.Length}:{eventId}";
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
    }

    public async Task<ProjectionDisposition> ExecuteAsync(Func<CancellationToken, Task> handler,
        string projection, string partition, long position, IReadOnlyList<ProjectionFailureEvent> events,
        ProjectionErrorHandlingOptions options, IProjectionFailurePolicy? policy = null,
        IProjectionDeadLetterStore? deadLetters = null, CancellationToken ct = default)
    {
        options.Validate();
        policy ??= options.FailurePolicy ?? new DefaultProjectionFailurePolicy(options);
        deadLetters ??= options.DeadLetterStore;
        if (deadLetters != null && options.SerializeDeadLetterEvent != null && events.Count > 0)
        {
            var recovered = 0;
            foreach (var item in events)
            {
                if (string.IsNullOrWhiteSpace(item.EventId)) continue;
                var existing = await deadLetters.GetAsync(GetDeadLetterId(projection, partition, item.EventId!), ct).ConfigureAwait(false);
                if (existing == null) continue;
                if (existing.Event != options.SerializeDeadLetterEvent(item.Value))
                    throw new InvalidOperationException("Dead-letter identity collision with different event data.");
                recovered++;
            }
            // A partially persisted batch is retried as a whole; handlers must be idempotent.
            if (recovered == events.Count) return ProjectionDisposition.Quarantined;
        }
        var delay = options.InitialRetryDelayMs;
        for (var attempt = 1; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await handler(ct).ConfigureAwait(false);
                return ProjectionDisposition.Projected;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                ct.ThrowIfCancellationRequested();
                var context = new ProjectionFailureContext(projection, partition, position, events, exception, attempt);
                var decision = await policy.DecideAsync(context, ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                // Even custom policies cannot cause unbounded retries.
                if (decision == ProjectionErrorHandlingStrategy.Retry && attempt >= options.MaxRetryAttempts)
                    decision = options.FallbackStrategy;
                switch (decision)
                {
                    case ProjectionErrorHandlingStrategy.Retry:
                        await Task.Delay(delay, ct).ConfigureAwait(false);
                        delay = (int)Math.Min((long)delay * 2, options.MaxRetryDelayMs);
                        break;
                    case ProjectionErrorHandlingStrategy.Skip:
                        return ProjectionDisposition.Skipped;
                    case ProjectionErrorHandlingStrategy.Quarantine:
                        if (deadLetters == null || options.SerializeDeadLetterEvent == null)
                            throw new InvalidOperationException("Quarantine requires a durable store and event serializer.");
                        foreach (var item in events)
                        {
                            if (string.IsNullOrWhiteSpace(item.EventId)) throw new InvalidOperationException("Quarantine requires a stable event ID.");
                            var entry = new ProjectionDeadLetter(GetDeadLetterId(projection, partition, item.EventId!),
                                projection, partition, item.EventId!, position, options.SerializeDeadLetterEvent(item.Value),
                                exception.GetType().FullName ?? "ProjectionFailure", attempt, DateTimeOffset.UtcNow);
                            await deadLetters.PutAsync(entry, ct).ConfigureAwait(false);
                        }
                        ct.ThrowIfCancellationRequested();
                        return ProjectionDisposition.Quarantined;
                    default:
                        return ProjectionDisposition.Stopped;
                }
            }
        }
    }
}
