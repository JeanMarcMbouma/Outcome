using System.Runtime.CompilerServices;

namespace BbQ.Outcome
{
    /// <summary>
    /// Extension methods for working with <see cref="IAsyncEnumerable{T}"/> streams of <see cref="Outcome{T}"/> values.
    /// These methods provide functional composition over async streams, enabling railway-oriented
    /// programming patterns on sequences of outcomes:
    /// - Select/Map: Transform successful values in the stream
    /// - SelectMany/Bind: Monadic bind (flatMap) over the stream
    /// - Where: Filter stream items by value predicate
    /// - Values: Extract only successful values, discarding errors
    /// - Errors: Extract only error lists, discarding successes
    /// </summary>
    public static class OutcomeAsyncEnumerableExtensions
    {
        // C# 14 extension type for instance methods on IAsyncEnumerable<Outcome<T>>
        extension<T>(IAsyncEnumerable<Outcome<T>> source)
        {
            /// <summary>
            /// Transforms each successful value in the async stream using <paramref name="selector"/>.
            /// Error outcomes are propagated unchanged without invoking the selector.
            /// This is the LINQ Select equivalent for async streams of outcomes.
            /// </summary>
            /// <typeparam name="TResult">The result type produced by the selector.</typeparam>
            /// <param name="selector">A function that transforms the successful value. Must not be null.</param>
            /// <param name="cancellationToken">A token to cancel the enumeration.</param>
            /// <returns>An async stream of outcomes with transformed success values.</returns>
            /// <example>
            /// <code>
            /// var doubled = stream.Select(x => x * 2);
            /// await foreach (var item in doubled)
            /// {
            ///     // Each successful value is doubled; errors pass through
            /// }
            /// </code>
            /// </example>
            public async IAsyncEnumerable<Outcome<TResult>> Select<TResult>(
                Func<T, TResult> selector,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    yield return item.IsSuccess
                        ? Outcome<TResult>.From(selector(item.Value))
                        : Outcome<TResult>.FromErrors(item.Errors!);
                }
            }

            /// <summary>
            /// Monadic bind with projection over an async stream (LINQ SelectMany pattern).
            /// For each successful outcome, applies <paramref name="binder"/> to produce an intermediate outcome,
            /// then projects both the original and intermediate values using <paramref name="projector"/>.
            /// Error outcomes are propagated unchanged.
            /// </summary>
            /// <typeparam name="TIntermediate">The intermediate result type from the binder.</typeparam>
            /// <typeparam name="TResult">The final result type produced by the projector.</typeparam>
            /// <param name="binder">A function that transforms the value into a new outcome. Must not be null.</param>
            /// <param name="projector">A function that combines the original and intermediate values. Must not be null.</param>
            /// <param name="cancellationToken">A token to cancel the enumeration.</param>
            /// <returns>An async stream of outcomes with projected results.</returns>
            /// <example>
            /// <code>
            /// var result = stream.SelectMany(
            ///     x => Validate(x),
            ///     (x, validated) => new { Original = x, Validated = validated });
            /// </code>
            /// </example>
            public async IAsyncEnumerable<Outcome<TResult>> SelectMany<TIntermediate, TResult>(
                Func<T, Outcome<TIntermediate>> binder,
                Func<T, TIntermediate, TResult> projector,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    yield return item.IsSuccess
                        ? binder(item.Value).Select(intermediate => projector(item.Value, intermediate))
                        : Outcome<TResult>.FromErrors(item.Errors!);
                }
            }

            /// <summary>
            /// Filters the async stream of outcomes using a value predicate.
            /// Successful outcomes that satisfy the predicate pass through unchanged.
            /// Successful outcomes that fail the predicate become validation errors.
            /// Error outcomes are propagated unchanged without checking the predicate.
            /// </summary>
            /// <param name="predicate">A function that tests the successful value. Must not be null.</param>
            /// <param name="cancellationToken">A token to cancel the enumeration.</param>
            /// <returns>An async stream of outcomes filtered by the predicate.</returns>
            /// <example>
            /// <code>
            /// var positives = stream.Where(x => x > 0);
            /// await foreach (var item in positives)
            /// {
            ///     // Successes where x &gt; 0 pass through; others become validation errors
            /// }
            /// </code>
            /// </example>
            public async IAsyncEnumerable<Outcome<T>> Where(
                Func<T, bool> predicate,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    if (!item.IsSuccess)
                    {
                        yield return item;
                        continue;
                    }

                    yield return predicate(item.Value)
                        ? item
                        : Outcome<T>.Validation("FILTER_FAIL", "Predicate not satisfied");
                }
            }

            /// <summary>
            /// Monadic bind operation over an async stream (flatMap).
            /// For each successful outcome, applies <paramref name="binder"/> to produce a new outcome.
            /// Error outcomes are propagated unchanged without invoking the binder.
            /// </summary>
            /// <typeparam name="TResult">The result type of the binder.</typeparam>
            /// <param name="binder">A function that transforms the value into a new outcome. Must not be null.</param>
            /// <param name="cancellationToken">A token to cancel the enumeration.</param>
            /// <returns>An async stream of outcomes produced by the binder.</returns>
            /// <example>
            /// <code>
            /// var validated = stream.Bind(x => x > 0
            ///     ? Outcome&lt;int&gt;.From(x)
            ///     : Outcome&lt;int&gt;.Validation("NEG", "Must be positive"));
            /// </code>
            /// </example>
            public async IAsyncEnumerable<Outcome<TResult>> Bind<TResult>(
                Func<T, Outcome<TResult>> binder,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    yield return item.IsSuccess
                        ? binder(item.Value)
                        : Outcome<TResult>.FromErrors(item.Errors!);
                }
            }

            /// <summary>
            /// Functor map operation over an async stream.
            /// Transforms each successful value using <paramref name="mapper"/> while propagating errors.
            /// Functionally equivalent to <see cref="Select{TResult}"/>, provided for API consistency
            /// with the synchronous <c>Map</c> method on <see cref="Outcome{T}"/>.
            /// </summary>
            /// <typeparam name="TResult">The result type produced by the mapper.</typeparam>
            /// <param name="mapper">A function that transforms the successful value. Must not be null.</param>
            /// <param name="cancellationToken">A token to cancel the enumeration.</param>
            /// <returns>An async stream of outcomes with transformed success values.</returns>
            /// <example>
            /// <code>
            /// var doubled = stream.Map(x => x * 2);
            /// await foreach (var item in doubled)
            /// {
            ///     // Each successful value is doubled; errors pass through
            /// }
            /// </code>
            /// </example>
            public async IAsyncEnumerable<Outcome<TResult>> Map<TResult>(
                Func<T, TResult> mapper,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    yield return item.IsSuccess
                        ? Outcome<TResult>.From(mapper(item.Value))
                        : Outcome<TResult>.FromErrors(item.Errors!);
                }
            }

            /// <summary>
            /// Extracts only the successful values from the async stream, discarding error outcomes.
            /// This is useful when you want to process only the values that succeeded,
            /// ignoring any failures in the stream.
            /// </summary>
            /// <param name="cancellationToken">A token to cancel the enumeration.</param>
            /// <returns>An async stream of successful values only.</returns>
            /// <example>
            /// <code>
            /// await foreach (var value in stream.Values())
            /// {
            ///     // Only successful values arrive here; errors are silently skipped
            /// }
            /// </code>
            /// </example>
            public async IAsyncEnumerable<T> Values(
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    if (item.IsSuccess)
                        yield return item.Value;
                }
            }

            /// <summary>
            /// Extracts only the error lists from the async stream, discarding successful outcomes.
            /// This is useful for collecting or logging errors from a stream of outcomes.
            /// </summary>
            /// <param name="cancellationToken">A token to cancel the enumeration.</param>
            /// <returns>An async stream of error lists from failed outcomes.</returns>
            /// <example>
            /// <code>
            /// await foreach (var errors in stream.Errors())
            /// {
            ///     // Only error lists arrive here; successes are silently skipped
            /// }
            /// </code>
            /// </example>
            public async IAsyncEnumerable<IReadOnlyList<object?>> Errors(
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    if (item.IsError)
                        yield return item.Errors;
                }
            }
        }
    }
}
