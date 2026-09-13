using System.Runtime.CompilerServices;

namespace BbQ.Outcome;

/// <summary>
/// Token-aware async callbacks. Cancellation is checked before invoking a callback and
/// forwarded to it. Cancelling a wait on an existing Task does not cancel its producer.
/// </summary>
public static class OutcomeAsyncCompositionExtensions
{
    /// <summary>Awaits the asynchronous callback for the active branch.</summary>
    /// <example><code>
    /// var message = await result.MatchAsync(
    ///     (user, ct) => RenderUserAsync(user, ct),
    ///     (errors, ct) => RenderErrorsAsync(errors, ct),
    ///     cancellationToken);
    /// </code></example>
    public static async Task<TResult> MatchAsync<T, TError, TResult>(this Outcome<T, TError> outcome,
        Func<T, CancellationToken, Task<TResult>> onSuccess,
        Func<IReadOnlyList<TError>, CancellationToken, Task<TResult>> onError,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onError);
        cancellationToken.ThrowIfCancellationRequested();
        return outcome.IsSuccess
            ? await onSuccess(outcome.ValueUnchecked, cancellationToken).ConfigureAwait(false)
            : await onError(outcome.ErrorsUnchecked, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Awaits the source and the selected asynchronous branch callback.</summary>
    /// <example><code>
    /// var response = await service.LoadAsync(id, cancellationToken).MatchAsync(
    ///     (user, ct) => BuildResponseAsync(user, ct),
    ///     (errors, ct) => BuildFailureAsync(errors, ct),
    ///     cancellationToken);
    /// </code></example>
    public static async Task<TResult> MatchAsync<T, TError, TResult>(this Task<Outcome<T, TError>> task,
        Func<T, CancellationToken, Task<TResult>> onSuccess,
        Func<IReadOnlyList<TError>, CancellationToken, Task<TResult>> onError,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onError);
        var outcome = await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        return await outcome.MatchAsync(onSuccess, onError, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Maps a success with an asynchronous callback receiving the caller's token.</summary>
    /// <example><code>
    /// var dto = await result.MapAsync((user, ct) => mapper.MapAsync(user, ct), cancellationToken);
    /// </code></example>
    public static async Task<Outcome<TResult, TError>> MapAsync<T, TError, TResult>(this Outcome<T, TError> outcome,
        Func<T, CancellationToken, Task<TResult>> mapper, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        cancellationToken.ThrowIfCancellationRequested();
        return outcome.IsSuccess
            ? Outcome<TResult, TError>.From(await mapper(outcome.ValueUnchecked, cancellationToken).ConfigureAwait(false))
            : Outcome<TResult, TError>.FromErrors(outcome.ErrorsUnchecked);
    }

    /// <summary>Binds a success with an asynchronous callback receiving the caller's token.</summary>
    /// <example><code>
    /// var saved = await validated.BindAsync((user, ct) => repository.SaveAsync(user, ct), cancellationToken);
    /// </code></example>
    public static async Task<Outcome<TResult, TError>> BindAsync<T, TError, TResult>(this Outcome<T, TError> outcome,
        Func<T, CancellationToken, Task<Outcome<TResult, TError>>> binder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binder);
        cancellationToken.ThrowIfCancellationRequested();
        return outcome.IsSuccess
            ? await binder(outcome.ValueUnchecked, cancellationToken).ConfigureAwait(false)
            : Outcome<TResult, TError>.FromErrors(outcome.ErrorsUnchecked);
    }

    /// <summary>Awaits a source task and then maps its success.</summary>
    /// <example><code>
    /// var dto = await service.LoadAsync(id, cancellationToken)
    ///     .MapAsync((user, ct) => mapper.MapAsync(user, ct), cancellationToken);
    /// </code></example>
    public static async Task<Outcome<TResult, TError>> MapAsync<T, TError, TResult>(this Task<Outcome<T, TError>> task,
        Func<T, CancellationToken, Task<TResult>> mapper, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(mapper);
        var outcome = await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        return await outcome.MapAsync(mapper, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Awaits a source task and then binds its success.</summary>
    /// <example><code>
    /// var saved = await service.ValidateAsync(command, cancellationToken)
    ///     .BindAsync((user, ct) => repository.SaveAsync(user, ct), cancellationToken);
    /// </code></example>
    public static async Task<Outcome<TResult, TError>> BindAsync<T, TError, TResult>(this Task<Outcome<T, TError>> task,
        Func<T, CancellationToken, Task<Outcome<TResult, TError>>> binder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(binder);
        var outcome = await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        return await outcome.BindAsync(binder, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Observes a success asynchronously and returns the original outcome.</summary>
    /// <example><code>
    /// var result = await created.TapAsync((user, ct) => audit.RecordCreatedAsync(user.Id, ct), cancellationToken);
    /// </code></example>
    public static async Task<Outcome<T, TError>> TapAsync<T, TError>(this Outcome<T, TError> outcome,
        Func<T, CancellationToken, Task> observer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observer);
        cancellationToken.ThrowIfCancellationRequested();
        if (outcome.IsSuccess)
            await observer(outcome.ValueUnchecked, cancellationToken).ConfigureAwait(false);
        else
            _ = outcome.ErrorsUnchecked;
        return outcome;
    }

    /// <summary>Observes a failure asynchronously and returns the original outcome.</summary>
    /// <example><code>
    /// var result = await created.TapErrorAsync((errors, ct) => audit.RecordFailureAsync(errors, ct), cancellationToken);
    /// </code></example>
    public static async Task<Outcome<T, TError>> TapErrorAsync<T, TError>(this Outcome<T, TError> outcome,
        Func<IReadOnlyList<TError>, CancellationToken, Task> observer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observer);
        cancellationToken.ThrowIfCancellationRequested();
        if (!outcome.IsSuccess)
            await observer(outcome.ErrorsUnchecked, cancellationToken).ConfigureAwait(false);
        return outcome;
    }

    /// <summary>Runs an asynchronous alternative only on failure.</summary>
    /// <example><code>
    /// var result = await cacheResult.RecoverAsync((_, ct) => repository.LoadAsync(id, ct), cancellationToken);
    /// </code></example>
    public static async Task<Outcome<T, TError>> RecoverAsync<T, TError>(this Outcome<T, TError> outcome,
        Func<IReadOnlyList<TError>, CancellationToken, Task<Outcome<T, TError>>> recovery,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        cancellationToken.ThrowIfCancellationRequested();
        return outcome.IsSuccess ? outcome
            : await recovery(outcome.ErrorsUnchecked, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Awaits an asynchronous branch callback on a heterogeneous outcome.</summary>
    /// <example><code>
    /// var text = await result.MatchAsync(
    ///     (value, ct) => RenderAsync(value, ct),
    ///     (errors, ct) => RenderErrorsAsync(errors, ct), cancellationToken);
    /// </code></example>
    public static Task<TResult> MatchAsync<T, TResult>(this Outcome<T> outcome,
        Func<T, CancellationToken, Task<TResult>> onSuccess,
        Func<IReadOnlyList<object?>, CancellationToken, Task<TResult>> onError,
        CancellationToken cancellationToken = default)
        => OutcomeInterop.Typed(outcome).MatchAsync(onSuccess, onError, cancellationToken);

    /// <summary>Awaits a heterogeneous source task and its asynchronous branch callback.</summary>
    /// <example><code>
    /// var text = await service.LoadAsync(id, cancellationToken).MatchAsync(
    ///     (value, ct) => RenderAsync(value, ct),
    ///     (errors, ct) => RenderErrorsAsync(errors, ct), cancellationToken);
    /// </code></example>
    public static async Task<TResult> MatchAsync<T, TResult>(this Task<Outcome<T>> task,
        Func<T, CancellationToken, Task<TResult>> onSuccess,
        Func<IReadOnlyList<object?>, CancellationToken, Task<TResult>> onError,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onError);
        var outcome = await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        return await outcome.MatchAsync(onSuccess, onError, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Maps a heterogeneous success asynchronously with cancellation.</summary>
    /// <example><code>
    /// var dto = await result.MapAsync((user, ct) => mapper.MapAsync(user, ct), cancellationToken);
    /// </code></example>
    public static Task<Outcome<TResult>> MapAsync<T, TResult>(this Outcome<T> outcome,
        Func<T, CancellationToken, Task<TResult>> mapper, CancellationToken cancellationToken = default)
        => OutcomeInterop.UntypedAsync(OutcomeInterop.Typed(outcome).MapAsync(mapper, cancellationToken));

    /// <summary>Binds a heterogeneous success asynchronously with cancellation.</summary>
    /// <example><code>
    /// var saved = await result.BindAsync((user, ct) => repository.SaveAsync(user, ct), cancellationToken);
    /// </code></example>
    public static async Task<Outcome<TResult>> BindAsync<T, TResult>(this Outcome<T> outcome,
        Func<T, CancellationToken, Task<Outcome<TResult>>> binder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binder);
        cancellationToken.ThrowIfCancellationRequested();
        return outcome.IsSuccess ? await binder(outcome.ValueUnchecked, cancellationToken).ConfigureAwait(false)
            : Outcome<TResult>.FromErrors(outcome.ErrorsUnchecked);
    }

    /// <summary>Awaits a heterogeneous source task before mapping.</summary>
    /// <example><code>
    /// var dto = await service.LoadAsync(id, cancellationToken)
    ///     .MapAsync((user, ct) => mapper.MapAsync(user, ct), cancellationToken);
    /// </code></example>
    public static async Task<Outcome<TResult>> MapAsync<T, TResult>(this Task<Outcome<T>> task,
        Func<T, CancellationToken, Task<TResult>> mapper, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(mapper);
        var outcome = await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        return await outcome.MapAsync(mapper, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Awaits a heterogeneous source task before binding.</summary>
    /// <example><code>
    /// var saved = await service.LoadAsync(id, cancellationToken)
    ///     .BindAsync((user, ct) => repository.SaveAsync(user, ct), cancellationToken);
    /// </code></example>
    public static async Task<Outcome<TResult>> BindAsync<T, TResult>(this Task<Outcome<T>> task,
        Func<T, CancellationToken, Task<Outcome<TResult>>> binder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(binder);
        var outcome = await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        return await outcome.BindAsync(binder, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Observes a heterogeneous success asynchronously.</summary>
    /// <example><code>
    /// var observed = await result.TapAsync((user, ct) => audit.RecordAsync(user, ct), cancellationToken);
    /// </code></example>
    public static Task<Outcome<T>> TapAsync<T>(this Outcome<T> outcome,
        Func<T, CancellationToken, Task> observer, CancellationToken cancellationToken = default)
        => OutcomeInterop.UntypedAsync(OutcomeInterop.Typed(outcome).TapAsync(observer, cancellationToken));

    /// <summary>Observes a heterogeneous failure asynchronously.</summary>
    /// <example><code>
    /// var observed = await result.TapErrorAsync((errors, ct) => audit.RecordErrorsAsync(errors, ct), cancellationToken);
    /// </code></example>
    public static Task<Outcome<T>> TapErrorAsync<T>(this Outcome<T> outcome,
        Func<IReadOnlyList<object?>, CancellationToken, Task> observer, CancellationToken cancellationToken = default)
        => OutcomeInterop.UntypedAsync(OutcomeInterop.Typed(outcome).TapErrorAsync(observer, cancellationToken));

    /// <summary>Runs an asynchronous alternative on a heterogeneous failure.</summary>
    /// <example><code>
    /// var result = await cacheResult.RecoverAsync((_, ct) => repository.LoadAsync(id, ct), cancellationToken);
    /// </code></example>
    public static async Task<Outcome<T>> RecoverAsync<T>(this Outcome<T> outcome,
        Func<IReadOnlyList<object?>, CancellationToken, Task<Outcome<T>>> recovery,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        cancellationToken.ThrowIfCancellationRequested();
        return outcome.IsSuccess ? outcome
            : await recovery(outcome.ErrorsUnchecked, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sequentially maps successful stream items with asynchronous callbacks.</summary>
    /// <example><code>
    /// await foreach (var mapped in stream.MapAsync((user, ct) => mapper.MapAsync(user, ct), cancellationToken))
    ///     Consume(mapped);
    /// </code></example>
    public static IAsyncEnumerable<Outcome<TResult, TError>> MapAsync<T, TError, TResult>(
        this IAsyncEnumerable<Outcome<T, TError>> source,
        Func<T, CancellationToken, Task<TResult>> mapper, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(mapper);
        return Iterate(cancellationToken);
        async IAsyncEnumerable<Outcome<TResult, TError>> Iterate([EnumeratorCancellation] CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
                yield return await item.MapAsync(mapper, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Sequentially binds successful stream items with asynchronous callbacks.</summary>
    /// <example><code>
    /// await foreach (var saved in stream.BindAsync((user, ct) => repository.SaveAsync(user, ct), cancellationToken))
    ///     Consume(saved);
    /// </code></example>
    public static IAsyncEnumerable<Outcome<TResult, TError>> BindAsync<T, TError, TResult>(
        this IAsyncEnumerable<Outcome<T, TError>> source,
        Func<T, CancellationToken, Task<Outcome<TResult, TError>>> binder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(binder);
        return Iterate(cancellationToken);
        async IAsyncEnumerable<Outcome<TResult, TError>> Iterate([EnumeratorCancellation] CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
                yield return await item.BindAsync(binder, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Sequentially maps heterogeneous stream successes with asynchronous callbacks.</summary>
    /// <example><code>
    /// await foreach (var mapped in stream.MapAsync((user, ct) => mapper.MapAsync(user, ct), cancellationToken))
    ///     Consume(mapped);
    /// </code></example>
    public static IAsyncEnumerable<Outcome<TResult>> MapAsync<T, TResult>(this IAsyncEnumerable<Outcome<T>> source,
        Func<T, CancellationToken, Task<TResult>> mapper, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(mapper);
        return Iterate(cancellationToken);
        async IAsyncEnumerable<Outcome<TResult>> Iterate([EnumeratorCancellation] CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
                yield return await item.MapAsync(mapper, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Sequentially binds heterogeneous stream successes with asynchronous callbacks.</summary>
    /// <example><code>
    /// await foreach (var saved in stream.BindAsync((user, ct) => repository.SaveAsync(user, ct), cancellationToken))
    ///     Consume(saved);
    /// </code></example>
    public static IAsyncEnumerable<Outcome<TResult>> BindAsync<T, TResult>(this IAsyncEnumerable<Outcome<T>> source,
        Func<T, CancellationToken, Task<Outcome<TResult>>> binder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(binder);
        return Iterate(cancellationToken);
        async IAsyncEnumerable<Outcome<TResult>> Iterate([EnumeratorCancellation] CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
                yield return await item.BindAsync(binder, ct).ConfigureAwait(false);
        }
    }
}
