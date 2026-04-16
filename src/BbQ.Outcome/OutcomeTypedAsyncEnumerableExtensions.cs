using System.Runtime.CompilerServices;

namespace BbQ.Outcome
{
    /// <summary>
    /// Extension methods for working with <see cref="IAsyncEnumerable{T}"/> streams of <see cref="Outcome{T, TError}"/>.
    /// Mirrors <see cref="OutcomeAsyncEnumerableExtensions"/> but preserves the strongly-typed error type.
    /// </summary>
    public static class OutcomeTypedAsyncEnumerableExtensions
    {
        // C# 14 extension type for instance methods on IAsyncEnumerable<Outcome<T, TError>>
        extension<T, TError>(IAsyncEnumerable<Outcome<T, TError>> source)
        {
            /// <summary>
            /// Transforms each successful value in the async stream using <paramref name="selector"/>.
            /// Error outcomes are propagated unchanged with their typed errors.
            /// </summary>
            public async IAsyncEnumerable<Outcome<TResult, TError>> Select<TResult>(
                Func<T, TResult> selector,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    yield return item.IsSuccess
                        ? Outcome<TResult, TError>.From(selector(item.ValueUnchecked))
                        : Outcome<TResult, TError>.FromErrors(item.ErrorsUnchecked);
                }
            }

            /// <summary>
            /// Monadic bind with projection over an async stream (LINQ SelectMany pattern).
            /// </summary>
            public async IAsyncEnumerable<Outcome<TResult, TError>> SelectMany<TIntermediate, TResult>(
                Func<T, Outcome<TIntermediate, TError>> binder,
                Func<T, TIntermediate, TResult> projector,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    yield return item.IsSuccess
                        ? binder(item.ValueUnchecked).Select(intermediate => projector(item.ValueUnchecked, intermediate))
                        : Outcome<TResult, TError>.FromErrors(item.ErrorsUnchecked);
                }
            }

            /// <summary>
            /// Filters the async stream using a value predicate.
            /// Successful outcomes that fail the predicate become errors via <paramref name="onFilterFail"/>.
            /// </summary>
            public async IAsyncEnumerable<Outcome<T, TError>> Where(
                Func<T, bool> predicate,
                Func<T, TError> onFilterFail,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    if (!item.IsSuccess)
                    {
                        yield return item;
                        continue;
                    }

                    yield return predicate(item.ValueUnchecked)
                        ? item
                        : Outcome<T, TError>.FromError(onFilterFail(item.ValueUnchecked));
                }
            }

            /// <summary>
            /// Monadic bind operation over an async stream (flatMap).
            /// </summary>
            public async IAsyncEnumerable<Outcome<TResult, TError>> Bind<TResult>(
                Func<T, Outcome<TResult, TError>> binder,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    yield return item.IsSuccess
                        ? binder(item.ValueUnchecked)
                        : Outcome<TResult, TError>.FromErrors(item.ErrorsUnchecked);
                }
            }

            /// <summary>
            /// Functor map operation over an async stream.
            /// </summary>
            public async IAsyncEnumerable<Outcome<TResult, TError>> Map<TResult>(
                Func<T, TResult> mapper,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    yield return item.IsSuccess
                        ? Outcome<TResult, TError>.From(mapper(item.ValueUnchecked))
                        : Outcome<TResult, TError>.FromErrors(item.ErrorsUnchecked);
                }
            }

            /// <summary>
            /// Extracts only the successful values from the async stream, discarding errors.
            /// </summary>
            public async IAsyncEnumerable<T> Values(
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    if (item.IsSuccess)
                        yield return item.ValueUnchecked;
                }
            }

            /// <summary>
            /// Extracts only the typed error lists from the async stream, discarding successes.
            /// </summary>
            public async IAsyncEnumerable<IReadOnlyList<TError>> Errors(
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    if (item.IsError)
                        yield return item.ErrorsUnchecked;
                }
            }
        }
    }
}
