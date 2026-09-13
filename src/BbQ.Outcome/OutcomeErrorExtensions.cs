namespace BbQ.Outcome;

/// <summary>
/// Error translation, recovery and observation. Delegates are validated eagerly,
/// invoked only on their branch, and their exceptions are never swallowed.
/// </summary>
public static class OutcomeErrorExtensions
{
    /// <summary>Maps each error in order, changing the error type but not a success value.</summary>
    /// <example><code>
    /// var result = repository.Find(id)
    ///     .MapError(error => new ApiError(error.Code, error.Description));
    /// </code></example>
    public static Outcome<T, TNewError> MapError<T, TError, TNewError>(
        this Outcome<T, TError> outcome, Func<TError, TNewError> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        if (outcome.IsSuccess)
            return Outcome<T, TNewError>.From(outcome.ValueUnchecked);
        var errors = outcome.ErrorsUnchecked;
        var mapped = new TNewError[errors.Count];
        for (var i = 0; i < mapped.Length; i++)
            mapped[i] = mapper(errors[i]);
        return Outcome<T, TNewError>.FromErrors(mapped);
    }

    /// <summary>Maps the complete error list once. A null, empty, or null-containing result is rejected.</summary>
    /// <example><code>
    /// var result = validation.MapErrors(errors =>
    ///     errors.Select(error => new ApiError(error.Code, error.Description)).ToArray());
    /// </code></example>
    public static Outcome<T, TNewError> MapErrors<T, TError, TNewError>(
        this Outcome<T, TError> outcome,
        Func<IReadOnlyList<TError>, IReadOnlyList<TNewError>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return outcome.IsSuccess
            ? Outcome<T, TNewError>.From(outcome.ValueUnchecked)
            : Outcome<T, TNewError>.FromErrors(mapper(outcome.ErrorsUnchecked));
    }

    /// <summary>Runs an alternative operation once on failure; successes are unchanged.</summary>
    /// <example><code>
    /// var result = cache.Get(key)
    ///     .Recover(_ => database.Get(key));
    /// </code></example>
    public static Outcome<T, TError> Recover<T, TError>(
        this Outcome<T, TError> outcome,
        Func<IReadOnlyList<TError>, Outcome<T, TError>> recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        return outcome.IsSuccess ? outcome : recovery(outcome.ErrorsUnchecked);
    }

    /// <summary>Observes a success once and returns the original outcome.</summary>
    /// <example><code>
    /// var result = service.Create(command)
    ///     .Tap(user => audit.UserCreated(user.Id));
    /// </code></example>
    public static Outcome<T, TError> Tap<T, TError>(this Outcome<T, TError> outcome, Action<T> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        if (outcome.IsSuccess)
            observer(outcome.ValueUnchecked);
        else
            _ = outcome.ErrorsUnchecked;
        return outcome;
    }

    /// <summary>Observes the complete failure once and returns the original outcome.</summary>
    /// <example><code>
    /// var result = service.Create(command)
    ///     .TapError(errors => logger.LogWarning("Create failed with {Count} errors", errors.Count));
    /// </code></example>
    public static Outcome<T, TError> TapError<T, TError>(
        this Outcome<T, TError> outcome, Action<IReadOnlyList<TError>> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        if (!outcome.IsSuccess)
            observer(outcome.ErrorsUnchecked);
        return outcome;
    }

    /// <summary>Maps heterogeneous errors into a strongly typed error pipeline.</summary>
    /// <example><code>
    /// Outcome&lt;User&gt; result = repository.Find(id);
    /// var typed = result.MapError(error => MapApplicationError(error));
    /// </code></example>
    public static Outcome<T, TNewError> MapError<T, TNewError>(
        this Outcome<T> outcome, Func<object?, TNewError> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        if (outcome.IsSuccess)
            return Outcome<T, TNewError>.From(outcome.ValueUnchecked);
        var errors = outcome.ErrorsUnchecked;
        var mapped = new TNewError[errors.Count];
        for (var i = 0; i < mapped.Length; i++)
            mapped[i] = mapper(errors[i]);
        return Outcome<T, TNewError>.FromErrors(mapped);
    }

    /// <summary>Maps a heterogeneous error list into a nonempty strongly typed error list.</summary>
    /// <example><code>
    /// var typed = result.MapErrors(errors => errors.Select(MapApplicationError).ToArray());
    /// </code></example>
    public static Outcome<T, TNewError> MapErrors<T, TNewError>(
        this Outcome<T> outcome, Func<IReadOnlyList<object?>, IReadOnlyList<TNewError>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return outcome.IsSuccess
            ? Outcome<T, TNewError>.From(outcome.ValueUnchecked)
            : Outcome<T, TNewError>.FromErrors(mapper(outcome.ErrorsUnchecked));
    }

    /// <summary>Runs an alternative operation once on a heterogeneous failure.</summary>
    /// <example><code>
    /// Outcome&lt;User&gt; result = cache.Get(id).Recover(_ => repository.Find(id));
    /// </code></example>
    public static Outcome<T> Recover<T>(
        this Outcome<T> outcome, Func<IReadOnlyList<object?>, Outcome<T>> recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        return outcome.IsSuccess ? outcome : recovery(outcome.ErrorsUnchecked);
    }

    /// <summary>Observes a success once without changing a heterogeneous outcome.</summary>
    /// <example><code>
    /// var result = service.Load(id).Tap(user => metrics.RecordLoaded(user.Id));
    /// </code></example>
    public static Outcome<T> Tap<T>(this Outcome<T> outcome, Action<T> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        if (outcome.IsSuccess)
            observer(outcome.ValueUnchecked);
        else
            _ = outcome.ErrorsUnchecked;
        return outcome;
    }

    /// <summary>Observes the error list once without changing a heterogeneous outcome.</summary>
    /// <example><code>
    /// var result = service.Load(id).TapError(errors => diagnostics.RecordFailure(errors.Count));
    /// </code></example>
    public static Outcome<T> TapError<T>(this Outcome<T> outcome, Action<IReadOnlyList<object?>> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        if (!outcome.IsSuccess)
            observer(outcome.ErrorsUnchecked);
        return outcome;
    }
}
