using System.Runtime.CompilerServices;

namespace BbQ.Outcome;

/// <summary>
/// Token-aware async callbacks. Cancellation is checked before invoking a callback and
/// forwarded to it. Cancelling a wait on an existing Task does not cancel its producer.
/// </summary>
public static class OutcomeAsyncCompositionExtensions
{
    /// <summary>Awaits the asynchronous callback for the active branch.</summary>
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
    public static async Task<Outcome<TResult, TError>> MapAsync<T, TError, TResult>(this Task<Outcome<T, TError>> task,
        Func<T, CancellationToken, Task<TResult>> mapper, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(mapper);
        var outcome = await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        return await outcome.MapAsync(mapper, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Awaits a source task and then binds its success.</summary>
    public static async Task<Outcome<TResult, TError>> BindAsync<T, TError, TResult>(this Task<Outcome<T, TError>> task,
        Func<T, CancellationToken, Task<Outcome<TResult, TError>>> binder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(binder);
        var outcome = await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        return await outcome.BindAsync(binder, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Observes a success asynchronously and returns the original outcome.</summary>
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
    public static Task<TResult> MatchAsync<T, TResult>(this Outcome<T> outcome,
        Func<T, CancellationToken, Task<TResult>> onSuccess,
        Func<IReadOnlyList<object?>, CancellationToken, Task<TResult>> onError,
        CancellationToken cancellationToken = default)
        => OutcomeInterop.Typed(outcome).MatchAsync(onSuccess, onError, cancellationToken);

    /// <summary>Awaits a heterogeneous source task and its asynchronous branch callback.</summary>
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
    public static Task<Outcome<TResult>> MapAsync<T, TResult>(this Outcome<T> outcome,
        Func<T, CancellationToken, Task<TResult>> mapper, CancellationToken cancellationToken = default)
        => OutcomeInterop.UntypedAsync(OutcomeInterop.Typed(outcome).MapAsync(mapper, cancellationToken));

    /// <summary>Binds a heterogeneous success asynchronously with cancellation.</summary>
    public static async Task<Outcome<TResult>> BindAsync<T, TResult>(this Outcome<T> outcome,
        Func<T, CancellationToken, Task<Outcome<TResult>>> binder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binder);
        cancellationToken.ThrowIfCancellationRequested();
        return outcome.IsSuccess ? await binder(outcome.ValueUnchecked, cancellationToken).ConfigureAwait(false)
            : Outcome<TResult>.FromErrors(outcome.ErrorsUnchecked);
    }

    /// <summary>Awaits a heterogeneous source task before mapping.</summary>
    public static async Task<Outcome<TResult>> MapAsync<T, TResult>(this Task<Outcome<T>> task,
        Func<T, CancellationToken, Task<TResult>> mapper, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(mapper);
        var outcome = await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        return await outcome.MapAsync(mapper, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Awaits a heterogeneous source task before binding.</summary>
    public static async Task<Outcome<TResult>> BindAsync<T, TResult>(this Task<Outcome<T>> task,
        Func<T, CancellationToken, Task<Outcome<TResult>>> binder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(binder);
        var outcome = await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        return await outcome.BindAsync(binder, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Observes a heterogeneous success asynchronously.</summary>
    public static Task<Outcome<T>> TapAsync<T>(this Outcome<T> outcome,
        Func<T, CancellationToken, Task> observer, CancellationToken cancellationToken = default)
        => OutcomeInterop.UntypedAsync(OutcomeInterop.Typed(outcome).TapAsync(observer, cancellationToken));

    /// <summary>Observes a heterogeneous failure asynchronously.</summary>
    public static Task<Outcome<T>> TapErrorAsync<T>(this Outcome<T> outcome,
        Func<IReadOnlyList<object?>, CancellationToken, Task> observer, CancellationToken cancellationToken = default)
        => OutcomeInterop.UntypedAsync(OutcomeInterop.Typed(outcome).TapErrorAsync(observer, cancellationToken));

    /// <summary>Runs an asynchronous alternative on a heterogeneous failure.</summary>
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
