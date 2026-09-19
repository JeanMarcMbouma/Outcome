namespace BbQ.Outcome;

/// <summary>Single-enumeration collection composition with deterministic input ordering.</summary>
public static class OutcomeCollectionExtensions
{
    /// <summary>Collects values if all succeed; otherwise accumulates all errors in input order.</summary>
    /// <example><code>
    /// var result = new[] { LoadUser(1), LoadUser(2), LoadUser(3) }.Sequence();
    /// </code></example>
    public static Outcome<IReadOnlyList<T>, TError> Sequence<T, TError>(this IEnumerable<Outcome<T, TError>> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var builder = new OutcomeCollectionBuilder<T, TError>(source.TryGetNonEnumeratedCount(out var count) ? count : 0);
        foreach (var outcome in source)
            builder.Add(outcome);
        return builder.BuildReadOnly();
    }

    /// <summary>Collects heterogeneous outcomes, accumulating all errors in input order.</summary>
    /// <example><code>
    /// Outcome&lt;IReadOnlyList&lt;User&gt;&gt; result = users.Select(ValidateUser).Sequence();
    /// </code></example>
    public static Outcome<IReadOnlyList<T>> Sequence<T>(this IEnumerable<Outcome<T>> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var builder = new OutcomeCollectionBuilder<T, object?>(source.TryGetNonEnumeratedCount(out var count) ? count : 0);
        foreach (var outcome in source)
            builder.Add(OutcomeInterop.Typed(outcome));
        return OutcomeInterop.Untyped(builder.BuildReadOnly());
    }

    /// <summary>Combines differently typed values; failures are accumulated left then right.</summary>
    /// <example><code>
    /// var result = LoadUser(id).Zip(LoadPermissions(id));
    /// result.Tap(pair => Use(pair.Left, pair.Right));
    /// </code></example>
    public static Outcome<(TLeft Left, TRight Right), TError> Zip<TLeft, TRight, TError>(
        this Outcome<TLeft, TError> left, Outcome<TRight, TError> right)
    {
        if (left.IsSuccess && right.IsSuccess)
            return Outcome<(TLeft, TRight), TError>.From((left.ValueUnchecked, right.ValueUnchecked));
        if (left.IsSuccess)
            return Outcome<(TLeft, TRight), TError>.FromErrors(right.ErrorsUnchecked);
        if (right.IsSuccess)
            return Outcome<(TLeft, TRight), TError>.FromErrors(left.ErrorsUnchecked);
        var leftErrors = left.ErrorsUnchecked;
        var rightErrors = right.ErrorsUnchecked;
        var errors = new TError[leftErrors.Count + rightErrors.Count];
        for (var i = 0; i < leftErrors.Count; i++) errors[i] = leftErrors[i];
        for (var i = 0; i < rightErrors.Count; i++) errors[leftErrors.Count + i] = rightErrors[i];
        return Outcome<(TLeft, TRight), TError>.FromErrors(ErrorCollection<TError>.FromOwnedArray(errors));
    }

    /// <summary>Combines differently typed heterogeneous outcomes.</summary>
    /// <example><code>
    /// var result = LoadProfile(id).Zip(LoadSettings(id));
    /// </code></example>
    public static Outcome<(TLeft Left, TRight Right)> Zip<TLeft, TRight>(
        this Outcome<TLeft> left, Outcome<TRight> right)
        => OutcomeInterop.Untyped(OutcomeInterop.Typed(left).Zip(OutcomeInterop.Typed(right)));

    /// <summary>Runs every operation sequentially and accumulates all domain failures.</summary>
    /// <example><code>
    /// var result = userIds.Traverse(id => repository.Find(id));
    /// </code></example>
    public static Outcome<IReadOnlyList<TResult>, TError> Traverse<TInput, TResult, TError>(
        this IEnumerable<TInput> source, Func<TInput, Outcome<TResult, TError>> operation)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(operation);
        var builder = new OutcomeCollectionBuilder<TResult, TError>(source.TryGetNonEnumeratedCount(out var count) ? count : 0);
        foreach (var input in source)
            builder.Add(operation(input));
        return builder.BuildReadOnly();
    }

    /// <summary>Runs every heterogeneous operation sequentially.</summary>
    /// <example><code>
    /// var result = commands.Traverse(command => handler.Handle(command));
    /// </code></example>
    public static Outcome<IReadOnlyList<TResult>> Traverse<TInput, TResult>(
        this IEnumerable<TInput> source, Func<TInput, Outcome<TResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(operation);
        var builder = new OutcomeCollectionBuilder<TResult, object?>(source.TryGetNonEnumeratedCount(out var count) ? count : 0);
        foreach (var input in source)
            builder.Add(OutcomeInterop.Typed(operation(input)));
        return OutcomeInterop.Untyped(builder.BuildReadOnly());
    }

    /// <summary>
    /// Executes operation factories in bounded batches and accumulates domain failures in
    /// input order. This is not fail-fast for domain failures. Unexpected exceptions cancel
    /// sibling operations and all started tasks are observed before the method completes.
    /// Callbacks must cooperate with cancellation for prompt shutdown.
    /// </summary>
    /// <example><code>
    /// var result = await userIds.TraverseAsync(
    ///     (id, ct) => repository.FindAsync(id, ct),
    ///     maxConcurrency: 8,
    ///     cancellationToken);
    /// </code></example>
    public static async Task<Outcome<IReadOnlyList<TResult>, TError>> TraverseAsync<TInput, TResult, TError>(
        this IEnumerable<TInput> source,
        Func<TInput, CancellationToken, Task<Outcome<TResult, TError>>> operation,
        int maxConcurrency = 1, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrency, 1);
        cancellationToken.ThrowIfCancellationRequested();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pending = new List<Task<Outcome<TResult, TError>>>();
        var builder = new OutcomeCollectionBuilder<TResult, TError>(source.TryGetNonEnumeratedCount(out var count) ? count : 0);
        try
        {
            foreach (var input in source)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                pending.Add(Invoke(input));
                if (pending.Count == maxConcurrency)
                {
                    foreach (var result in await Task.WhenAll(pending).ConfigureAwait(false))
                        builder.Add(result);
                    pending.Clear();
                }
            }
            if (pending.Count != 0)
                foreach (var result in await Task.WhenAll(pending).ConfigureAwait(false))
                    builder.Add(result);
            cancellationToken.ThrowIfCancellationRequested();
            return builder.BuildReadOnly();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            CancelSiblings();
            await Task.WhenAll(pending).ConfigureAwait(false);
            throw;
        }
        catch
        {
            CancelSiblings();
            try { await Task.WhenAll(pending).ConfigureAwait(false); } catch { }
            throw;
        }

        async Task<Outcome<TResult, TError>> Invoke(TInput input)
        {
            try { return await operation(input, cancellation.Token).ConfigureAwait(false); }
            catch { CancelSiblings(); throw; }
        }

        void CancelSiblings()
        {
            try { cancellation.Cancel(); } catch (AggregateException) { }
        }
    }

    /// <summary>Bounded, ordered, accumulate-all traversal for heterogeneous outcomes.</summary>
    /// <example><code>
    /// var result = await jobs.TraverseAsync(
    ///     (job, ct) => RunJobAsync(job, ct),
    ///     maxConcurrency: 4,
    ///     cancellationToken);
    /// </code></example>
    public static async Task<Outcome<IReadOnlyList<TResult>>> TraverseAsync<TInput, TResult>(
        this IEnumerable<TInput> source,
        Func<TInput, CancellationToken, Task<Outcome<TResult>>> operation,
        int maxConcurrency = 1, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var typed = await TraverseAsync<TInput, TResult, object?>(source,
            async (item, ct) => OutcomeInterop.Typed(await operation(item, ct).ConfigureAwait(false)),
            maxConcurrency, cancellationToken).ConfigureAwait(false);
        return OutcomeInterop.Untyped(typed);
    }
}
