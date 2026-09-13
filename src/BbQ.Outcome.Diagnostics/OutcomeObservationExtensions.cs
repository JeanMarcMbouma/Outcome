using System.Diagnostics;

namespace BbQ.Outcome.Diagnostics;

/// <summary>Pipeline-friendly opt-in observation for both Outcome forms.</summary>
public static class OutcomeObservationExtensions
{
    /// <summary>Records a typed outcome with the supplied observer and returns the original outcome unchanged.</summary>
    /// <example><code>
    /// var result = service.Create(command)
    ///     .Observe(observer, "users.create");
    /// </code></example>
    public static Outcome<T, TError> Observe<T, TError>(this Outcome<T, TError> outcome,
        OutcomeObserver<TError> observer, string operationName)
    {
        ArgumentNullException.ThrowIfNull(observer);
        return observer.Observe(outcome, operationName);
    }

    /// <summary>Records a heterogeneous outcome without changing its value or errors.</summary>
    /// <example><code>
    /// Outcome&lt;User&gt; result = service.GetUser(id)
    ///     .Observe(observer, "users.get");
    /// </code></example>
    public static Outcome<T> Observe<T>(this Outcome<T> outcome, OutcomeObserver<object?> observer, string operationName)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var typed = outcome.IsSuccess ? Outcome<T, object?>.From(outcome.Value) : Outcome<T, object?>.FromErrors(outcome.Errors);
        observer.Observe(typed, operationName);
        return outcome;
    }

    /// <summary>Runs a heterogeneous Outcome operation inside an Activity and records only the configured safe diagnostics.</summary>
    /// <example><code>
    /// var result = await observer.TraceAsync(
    ///     activitySource,
    ///     "users.load",
    ///     ct => service.GetUserAsync(id, ct),
    ///     cancellationToken);
    /// </code></example>
    public static async Task<Outcome<T>> TraceAsync<T>(this OutcomeObserver<object?> observer, ActivitySource source,
        string operationName, Func<CancellationToken, Task<Outcome<T>>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(operation);
        var result = await observer.TraceAsync(source, operationName, async ct =>
        {
            var outcome = await operation(ct).ConfigureAwait(false);
            return outcome.IsSuccess ? Outcome<T, object?>.From(outcome.Value) : Outcome<T, object?>.FromErrors(outcome.Errors);
        }, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Outcome<T>.From(result.Value) : Outcome<T>.FromErrors(result.Errors);
    }
}
