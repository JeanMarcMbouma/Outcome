using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BbQ.Outcome.Diagnostics;

/// <summary>Observation policy. Error descriptions, values and metadata are never recorded automatically.</summary>
public sealed record OutcomeObservationOptions(bool IncludeErrorCodes = false, int MaxErrorCodes = 16, int MaxCodeLength = 128);

/// <summary>Opt-in observation through ILogger and Activity, without owning a telemetry exporter.</summary>
public sealed class OutcomeObserver<TError>
{
    private readonly IErrorDescriptorProvider<TError>? _descriptors;
    private readonly ILogger? _logger;
    private readonly OutcomeObservationOptions _options;
    private static readonly Action<ILogger, string, bool, int, Exception?> LogOutcome =
        LoggerMessage.Define<string, bool, int>(LogLevel.Information, new EventId(6000, "OutcomeCompleted"),
            "Outcome {Operation} completed: success={Success}, errorCount={ErrorCount}");
    private static readonly Action<ILogger, string, Exception?> LogFault =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(6001, "OutcomeFaulted"),
            "Outcome {Operation} raised an unexpected exception");

    public OutcomeObserver(ILogger? logger = null, IErrorDescriptorProvider<TError>? descriptors = null,
        OutcomeObservationOptions? options = null)
    {
        _options = options ?? new();
        ArgumentOutOfRangeException.ThrowIfLessThan(_options.MaxErrorCodes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(_options.MaxCodeLength, 1);
        if (_options.IncludeErrorCodes && descriptors is null)
            throw new ArgumentException("Including error codes requires an explicit descriptor provider.", nameof(descriptors));
        _logger = logger;
        _descriptors = descriptors;
    }

    /// <summary>Annotates the current activity and logs counts; returns the original outcome.</summary>
    public Outcome<T, TError> Observe<T>(Outcome<T, TError> outcome, string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        Record(outcome, operationName, Activity.Current);
        return outcome;
    }

    /// <summary>
    /// Creates a child activity for the operation when a listener requests one. Domain failure
    /// sets Error status without throwing. Cancellation remains cancellation, and unexpected
    /// exceptions are rethrown without logging their messages or exception objects.
    /// </summary>
    public async Task<Outcome<T, TError>> TraceAsync<T>(ActivitySource source, string operationName,
        Func<CancellationToken, Task<Outcome<T, TError>>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        using var activity = source.StartActivity(operationName, ActivityKind.Internal);
        try
        {
            var outcome = await operation(cancellationToken).ConfigureAwait(false);
            Record(outcome, operationName, activity);
            return outcome;
        }
        catch (OperationCanceledException)
        {
            activity?.SetTag("outcome.cancelled", true);
            throw;
        }
        catch (Exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Unexpected exception");
            if (_logger is not null)
                LogFault(_logger, operationName, null);
            throw;
        }
    }

    private void Record<T>(Outcome<T, TError> outcome, string operationName, Activity? activity)
    {
        var errors = outcome.IsSuccess ? null : outcome.Errors;
        var count = errors?.Count ?? 0;
        activity?.SetTag("outcome.success", outcome.IsSuccess);
        activity?.SetTag("outcome.error_count", count);
        activity?.SetStatus(outcome.IsSuccess ? ActivityStatusCode.Ok : ActivityStatusCode.Error,
            outcome.IsSuccess ? null : "Outcome failure");
        if (activity is not null && errors is not null && _options.IncludeErrorCodes)
        {
            var codes = new string[Math.Min(errors.Count, _options.MaxErrorCodes)];
            for (var i = 0; i < codes.Length; i++)
            {
                var descriptor = _descriptors!.Describe(errors[i])
                    ?? throw new InvalidOperationException("The descriptor provider returned null.");
                var code = descriptor.Code ?? throw new InvalidOperationException("An error code cannot be null.");
                codes[i] = code.Length <= _options.MaxCodeLength ? code : code[.._options.MaxCodeLength];
            }
            activity.SetTag("outcome.error_codes", codes);
        }
        if (_logger is not null)
            LogOutcome(_logger, operationName, outcome.IsSuccess, count, null);
    }
}
