using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Outcome
{
    /// <summary>
    /// LINQ-style extension methods for working with <see cref="Outcome{T}"/> values.
    /// These methods provide functional composition using Select, SelectMany, and Where patterns
    /// familiar to developers using LINQ, enabling intuitive chaining of operations.
    /// Both synchronous and asynchronous (Task-based) variants are provided.
    /// </summary>
    public static class OutcomeLinqExtensions
    {
        // C# 14 extension type for instance methods on Outcome<T>
        extension<T>(Outcome<T> outcome)
        {
            /// <summary>
            /// Transforms the successful value using <paramref name="selector"/> while propagating errors.
            /// This is the LINQ Select equivalent, enabling functional mapping over outcomes.
            /// If the outcome is an error, the selector is not invoked and errors propagate unchanged.
            /// </summary>
            /// <typeparam name="TResult">The result type produced by the selector.</typeparam>
            /// <param name="selector">A function that transforms the successful value. Must not be null.</param>
            /// <returns>
            /// A success outcome with the transformed value (if the input is successful),
            /// or a failure outcome with the same errors (if the input is a failure).
            /// </returns>
            /// <example>
            /// <code>
            /// var doubled = from x in ParseInt("21")
            ///               select x * 2;
            /// // If success: 42; if error: propagates
            /// </code>
            /// </example>
            public Outcome<TResult> Select<TResult>(Func<T, TResult> selector)
                => outcome.IsSuccess
                    ? Outcome<TResult>.From(selector(outcome.Value))
                    : Outcome<TResult>.FromErrors(outcome.Errors!);

            /// <summary>
            /// Monadic bind with projection, implementing the LINQ SelectMany (flatMap) pattern.
            /// Chains a <paramref name="binder"/> that produces an <see cref="Outcome{TIntermediate}"/>,
            /// then projects both the original and intermediate values into a final result using <paramref name="projector"/>.
            /// </summary>
            /// <typeparam name="TIntermediate">The intermediate result type from the binder.</typeparam>
            /// <typeparam name="TResult">The final result type produced by the projector.</typeparam>
            /// <param name="binder">A function that transforms the value into a new outcome. Must not be null.</param>
            /// <param name="projector">A function that combines the original and intermediate values. Must not be null.</param>
            /// <returns>
            /// A success outcome with the projected result (if all operations succeed),
            /// or a failure outcome with aggregated errors (if any operation fails).
            /// </returns>
            /// <example>
            /// <code>
            /// var result = from x in ParseInt("2")
            ///              from y in ParseInt("3")
            ///              select x + y;
            /// // Equivalent to: ParseInt("2").SelectMany(x => ParseInt("3"), (x, y) => x + y)
            /// </code>
            /// </example>
            public Outcome<TResult> SelectMany<TIntermediate, TResult>( 
                Func<T, Outcome<TIntermediate>> binder,
                Func<T, TIntermediate, TResult> projector)
                => outcome.IsSuccess
                    ? binder(outcome.Value).Select(intermediate => projector(outcome.Value, intermediate))
                    : Outcome<TResult>.FromErrors(outcome.Errors!);

            /// <summary>
            /// Filters the outcome using a predicate.
            /// If the outcome is successful and the predicate is satisfied, returns the original outcome unchanged.
            /// If the predicate fails, returns a validation error outcome.
            /// If the input is already an error, propagates the error without checking the predicate.
            /// </summary>
            /// <param name="predicate">A function that tests the successful value. Must not be null.</param>
            /// <returns>
            /// The original outcome (if successful and predicate passes),
            /// a validation error outcome (if successful but predicate fails),
            /// or the same error outcome (if input is a failure).
            /// </returns>
            /// <example>
            /// <code>
            /// var positive = from x in ParseInt("42")
            ///                where x > 0
            ///                select x;
            /// // If 42 > 0: success with 42; otherwise: validation error
            /// </code>
            /// </example>
            public Outcome<T> Where(Func<T, bool> predicate)
            {
                if (!outcome.IsSuccess) 
                    return outcome;
                // If predicate passes, keep the outcome; otherwise create a validation error
                return predicate(outcome.Value!) ? outcome : Outcome<T>.Validation("FILTER_FAIL", "Predicate not satisfied");
            }
        }

        // C# 14 extension type for async composition on Task<Outcome<T>>
        extension<T>(Task<Outcome<T>> task)
        {
            /// <summary>
            /// Asynchronously transforms the successful value using a synchronous <paramref name="selector"/>.
            /// Awaits the task to obtain the outcome, then applies Select to its result.
            /// If the outcome is an error, the selector is not invoked and errors propagate unchanged.
            /// </summary>
            /// <typeparam name="TResult">The result type produced by the selector.</typeparam>
            /// <param name="selector">A function that transforms the successful value. Must not be null.</param>
            /// <returns>
            /// A task that represents the asynchronous selection operation.
            /// Resolves to a success outcome with the transformed value, or a failure outcome with errors.
            /// </returns>
            /// <example>
            /// <code>
            /// var doubled = from x in FetchIntAsync()
            ///               select x * 2;
            /// </code>
            /// </example>
            public async Task<Outcome<TResult>> Select<TResult>(Func<T, TResult> selector)
            {
                var outcome = await task.ConfigureAwait(false);
                return outcome.IsSuccess
                    ? Outcome<TResult>.From(selector(outcome.Value))
                    : Outcome<TResult>.FromErrors(outcome.Errors!);
            }

            /// <summary>
            /// Asynchronously applies SelectMany, supporting async composition chains.
            /// Awaits the task, then uses the result to invoke the binder and projector.
            /// Enables fluent composition of multiple async operations using LINQ query syntax.
            /// </summary>
            /// <typeparam name="TIntermediate">The intermediate result type from the binder.</typeparam>
            /// <typeparam name="TResult">The final result type produced by the projector.</typeparam>
            /// <param name="binder">A function that transforms the value into a new outcome. Must not be null.</param>
            /// <param name="projector">A function that combines the original and intermediate values. Must not be null.</param>
            /// <returns>
            /// A task that represents the asynchronous bind operation.
            /// Resolves to a success outcome with the projected result, or a failure outcome with aggregated errors.
            /// </returns>
            /// <example>
            /// <code>
            /// var result = from x in FetchIntAsync()
            ///              from y in FetchIntAsync()
            ///              select x + y;
            /// </code>
            /// </example>
            public async Task<Outcome<TResult>> SelectMany<TIntermediate, TResult>(
                Func<T, Outcome<TIntermediate>> binder,
                Func<T, TIntermediate, TResult> projector)
            {
                var outcome = await task.ConfigureAwait(false);
                return outcome.IsSuccess
                    ? binder(outcome.Value).Select(intermediate => projector(outcome.Value, intermediate))
                    : Outcome<TResult>.FromErrors(outcome.Errors!);
            }

            /// <summary>
            /// Asynchronously filters an outcome using a synchronous predicate.
            /// Awaits the task to obtain the outcome, then applies the Where filter.
            /// If the predicate fails, returns a validation error outcome.
            /// </summary>
            /// <param name="predicate">A function that tests the successful value. Must not be null.</param>
            /// <returns>
            /// A task that represents the asynchronous filter operation.
            /// Resolves to the original outcome (if successful and predicate passes),
            /// a validation error outcome (if successful but predicate fails),
            /// or the same error outcome (if input is a failure).
            /// </returns>
            /// <example>
            /// <code>
            /// var positive = from x in FetchIntAsync()
            ///                where x > 0
            ///                select x;
            /// </code>
            /// </example>
            public async Task<Outcome<T>> Where(Func<T, bool> predicate)
            {
                var result = await task;
                if (!result.IsSuccess) 
                    return result;
                // Apply predicate; return validation error if predicate fails
                return predicate(result.Value)
                    ? result
                    : Outcome<T>.Validation("FILTER_FAIL", "Predicate not satisfied");
            }
        }
    }
}
