namespace BbQ.Outcome
{
    /// <summary>
    /// LINQ-style extension methods for <see cref="Outcome{T, TError}"/>.
    /// Provides Select, SelectMany, and Where patterns that preserve the error type.
    /// </summary>
    public static class OutcomeTypedLinqExtensions
    {
        // C# 14 extension type for instance methods on Outcome<T, TError>
        extension<T, TError>(Outcome<T, TError> outcome)
        {
            /// <summary>
            /// Transforms the successful value using <paramref name="selector"/> while propagating typed errors.
            /// This is the LINQ Select equivalent.
            /// </summary>
            public Outcome<TResult, TError> Select<TResult>(Func<T, TResult> selector)
                => outcome.IsSuccess
                    ? Outcome<TResult, TError>.From(selector(outcome.ValueUnchecked))
                    : Outcome<TResult, TError>.FromErrors(outcome.ErrorsUnchecked);

            /// <summary>
            /// Monadic bind with projection, implementing the LINQ SelectMany (flatMap) pattern.
            /// The error type is preserved through the chain.
            /// </summary>
            public Outcome<TResult, TError> SelectMany<TIntermediate, TResult>(
                Func<T, Outcome<TIntermediate, TError>> binder,
                Func<T, TIntermediate, TResult> projector)
                => outcome.IsSuccess
                    ? binder(outcome.ValueUnchecked).Select(intermediate => projector(outcome.ValueUnchecked, intermediate))
                    : Outcome<TResult, TError>.FromErrors(outcome.ErrorsUnchecked);

            /// <summary>
            /// Filters the outcome using a predicate. If the predicate fails, invokes
            /// <paramref name="onFilterFail"/> to create the error.
            /// </summary>
            /// <param name="predicate">A function that tests the successful value.</param>
            /// <param name="onFilterFail">A factory that creates an error when the predicate fails.</param>
            public Outcome<T, TError> Where(Func<T, bool> predicate, Func<T, TError> onFilterFail)
            {
                if (!outcome.IsSuccess)
                    return outcome;
                return predicate(outcome.ValueUnchecked)
                    ? outcome
                    : Outcome<T, TError>.FromError(onFilterFail(outcome.ValueUnchecked));
            }
        }

        // C# 14 extension type for async composition on Task<Outcome<T, TError>>
        extension<T, TError>(Task<Outcome<T, TError>> task)
        {
            /// <summary>
            /// Asynchronously transforms the successful value using a synchronous selector.
            /// </summary>
            public async Task<Outcome<TResult, TError>> Select<TResult>(Func<T, TResult> selector)
            {
                var outcome = await task.ConfigureAwait(false);
                return outcome.IsSuccess
                    ? Outcome<TResult, TError>.From(selector(outcome.ValueUnchecked))
                    : Outcome<TResult, TError>.FromErrors(outcome.ErrorsUnchecked);
            }

            /// <summary>
            /// Asynchronously applies SelectMany, supporting async composition chains.
            /// </summary>
            public async Task<Outcome<TResult, TError>> SelectMany<TIntermediate, TResult>(
                Func<T, Outcome<TIntermediate, TError>> binder,
                Func<T, TIntermediate, TResult> projector)
            {
                var outcome = await task.ConfigureAwait(false);
                return outcome.IsSuccess
                    ? binder(outcome.ValueUnchecked).Select(intermediate => projector(outcome.ValueUnchecked, intermediate))
                    : Outcome<TResult, TError>.FromErrors(outcome.ErrorsUnchecked);
            }

            /// <summary>
            /// Asynchronously filters an outcome using a synchronous predicate.
            /// </summary>
            public async Task<Outcome<T, TError>> Where(Func<T, bool> predicate, Func<T, TError> onFilterFail)
            {
                var result = await task.ConfigureAwait(false);
                if (!result.IsSuccess)
                    return result;
                return predicate(result.ValueUnchecked)
                    ? result
                    : Outcome<T, TError>.FromError(onFilterFail(result.ValueUnchecked));
            }
        }
    }
}
