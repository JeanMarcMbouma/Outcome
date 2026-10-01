namespace BbQ.Outcome;

public static partial class Outcome
{
    /// <summary>
    /// Re-executes an asynchronous operation while it returns retryable typed failures.
    /// </summary>
    /// <remarks>
    /// The operation factory is invoked once per attempt. Exceptions are not caught or converted;
    /// compose with <see cref="TryAsync{T, TError}(Func{CancellationToken, Task{T}}, ExceptionMapper{TError}, CancellationToken)"/>
    /// when calling an exception-based dependency. Cancellation is propagated immediately.
    /// </remarks>
    /// <example><code>
    /// var result = await Outcome.RetryAsync(
    ///     ct => repository.LoadAsync(id, ct),
    ///     errors => errors.All(IsTransient),
    ///     new RetryOptions
    ///     {
    ///         MaxAttempts = 4,
    ///         DelayGenerator = retry => TimeSpan.FromMilliseconds(100 * retry)
    ///     },
    ///     cancellationToken);
    /// </code></example>
    public static async Task<Outcome<T, TError>> RetryAsync<T, TError>(
        Func<CancellationToken, Task<Outcome<T, TError>>> operation,
        Func<IReadOnlyList<TError>, bool> shouldRetry,
        RetryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(shouldRetry);

        options ??= new RetryOptions();
        options.Validate();

        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var outcome = await operation(cancellationToken).ConfigureAwait(false);
            if (outcome.IsSuccess)
                return outcome;

            var errors = outcome.ErrorsUnchecked;
            if (attempt >= options.MaxAttempts || !shouldRetry(errors))
                return outcome;

            var delay = options.GetDelay(attempt);
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Re-executes an asynchronous operation while it returns retryable heterogeneous failures.
    /// </summary>
    /// <remarks>
    /// The operation factory is invoked once per attempt. Exceptions are not caught or converted,
    /// and cancellation is propagated immediately.
    /// </remarks>
    public static async Task<Outcome<T>> RetryAsync<T>(
        Func<CancellationToken, Task<Outcome<T>>> operation,
        Func<IReadOnlyList<object?>, bool> shouldRetry,
        RetryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(shouldRetry);

        options ??= new RetryOptions();
        options.Validate();

        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var outcome = await operation(cancellationToken).ConfigureAwait(false);
            if (outcome.IsSuccess)
                return outcome;

            var errors = outcome.ErrorsUnchecked;
            if (attempt >= options.MaxAttempts || !shouldRetry(errors))
                return outcome;

            var delay = options.GetDelay(attempt);
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }
}
