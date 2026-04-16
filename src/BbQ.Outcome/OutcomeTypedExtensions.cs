namespace BbQ.Outcome
{
    /// <summary>
    /// Extension methods that provide functional composition patterns for <see cref="Outcome{T, TError}"/> values.
    /// These mirror the <see cref="OutcomeExtensions"/> for <see cref="Outcome{T}"/> but preserve the
    /// strongly-typed <typeparamref name="TError"/> through the entire pipeline, avoiding boxing.
    /// </summary>
    public static class OutcomeTypedExtensions
    {
        // C# 14 extension type for instance methods on Outcome<T, TError>
        extension<T, TError>(Outcome<T, TError> outcome)
        {
            /// <summary>
            /// Pattern-matches over the outcome: if successful, invokes <paramref name="onSuccess"/>,
            /// otherwise invokes <paramref name="onError"/> with the strongly-typed error list.
            /// </summary>
            public TResult Match<TResult>(
                Func<T, TResult> onSuccess,
                Func<IReadOnlyList<TError>, TResult> onError)
            {
                return outcome.IsSuccess
                    ? onSuccess(outcome.ValueUnchecked)
                    : onError(outcome.ErrorsUnchecked);
            }

            /// <summary>
            /// Executes one of two actions depending on whether the outcome is success or error.
            /// </summary>
            public void Switch(
                Action<T> onSuccess,
                Action<IReadOnlyList<TError>> onError)
            {
                if (outcome.IsSuccess)
                    onSuccess(outcome.ValueUnchecked);
                else
                    onError(outcome.ErrorsUnchecked);
            }

            /// <summary>
            /// Monadic bind operation. When the outcome is successful, applies <paramref name="binder"/>
            /// to produce a new <see cref="Outcome{TResult, TError}"/>. The error type is preserved.
            /// </summary>
            public Outcome<TResult, TError> Bind<TResult>(Func<T, Outcome<TResult, TError>> binder)
            {
                return outcome.IsSuccess
                    ? binder(outcome.ValueUnchecked)
                    : Outcome<TResult, TError>.FromErrors(outcome.ErrorsUnchecked);
            }

            /// <summary>
            /// Functor map operation. Transforms the successful value while preserving the error type.
            /// </summary>
            public Outcome<TResult, TError> Map<TResult>(Func<T, TResult> mapper)
            {
                return outcome.IsSuccess
                    ? Outcome<TResult, TError>.From(mapper(outcome.ValueUnchecked))
                    : Outcome<TResult, TError>.FromErrors(outcome.ErrorsUnchecked);
            }

            /// <summary>
            /// Combines multiple typed outcomes into a single outcome containing an enumerable of values.
            /// If any input is an error, aggregates and returns all errors.
            /// </summary>
            public static Outcome<IEnumerable<T>, TError> Combine(params Outcome<T, TError>[] outcomes)
            {
                List<TError>? errors = null;
                List<T>? values = null;

                foreach (var item in outcomes)
                {
                    if (item.IsSuccess)
                    {
                        values ??= new List<T>(outcomes.Length);
                        values.Add(item.ValueUnchecked);
                    }
                    else
                    {
                        errors ??= [];
                        var itemErrors = item.ErrorsUnchecked;
                        for (var i = 0; i < itemErrors.Count; i++)
                        {
                            errors.Add(itemErrors[i]);
                        }
                    }
                }

                if (errors is { Count: > 0 })
                {
                    return Outcome<IEnumerable<T>, TError>.FromErrors(errors);
                }

                return Outcome<IEnumerable<T>, TError>.From(values ?? (IEnumerable<T>)Array.Empty<T>());
            }

            // ============ Async composition methods ============

            /// <summary>
            /// Asynchronously maps the successful value using an async mapper function.
            /// The error type is preserved through the async operation.
            /// </summary>
            public Task<Outcome<TResult, TError>> MapAsync<TResult>(Func<T, Task<TResult>> mapper)
            {
                if (!outcome.IsSuccess)
                {
                    return Task.FromResult(Outcome<TResult, TError>.FromErrors(outcome.ErrorsUnchecked));
                }

                return AwaitMapAsync(outcome.ValueUnchecked, mapper);

                static async Task<Outcome<TResult, TError>> AwaitMapAsync(T value, Func<T, Task<TResult>> map)
                {
                    return Outcome<TResult, TError>.From(await map(value).ConfigureAwait(false));
                }
            }

            /// <summary>
            /// Asynchronously binds the successful value using an async binder.
            /// The error type is preserved through the async operation.
            /// </summary>
            public Task<Outcome<TResult, TError>> BindAsync<TResult>(Func<T, Task<Outcome<TResult, TError>>> binder)
            {
                if (!outcome.IsSuccess)
                {
                    return Task.FromResult(Outcome<TResult, TError>.FromErrors(outcome.ErrorsUnchecked));
                }

                return AwaitBindAsync(outcome.ValueUnchecked, binder);

                static async Task<Outcome<TResult, TError>> AwaitBindAsync(T value, Func<T, Task<Outcome<TResult, TError>>> bind)
                {
                    return await bind(value).ConfigureAwait(false);
                }
            }

            /// <summary>
            /// Awaits multiple typed outcome-producing tasks and combines their results.
            /// </summary>
            public static async Task<Outcome<IEnumerable<T>, TError>> CombineAsync(params Task<Outcome<T, TError>>[] tasks)
            {
                var results = await Task.WhenAll(tasks).ConfigureAwait(false);

                List<TError>? errors = null;
                var values = new T[results.Length];
                var valueCount = 0;

                for (var i = 0; i < results.Length; i++)
                {
                    var result = results[i];

                    if (result.IsSuccess)
                    {
                        values[valueCount++] = result.ValueUnchecked;
                    }
                    else
                    {
                        errors ??= [];
                        var resultErrors = result.ErrorsUnchecked;
                        for (var j = 0; j < resultErrors.Count; j++)
                        {
                            errors.Add(resultErrors[j]);
                        }
                    }
                }

                if (errors is { Count: > 0 })
                {
                    return Outcome<IEnumerable<T>, TError>.FromErrors(errors);
                }

                return Outcome<IEnumerable<T>, TError>.From(values);
            }
        }

        // C# 14 extension type for static factory methods on Outcome<T, TError>
        extension<T, TError>(Outcome<T, TError>)
        {
            /// <summary>
            /// Creates a failure outcome from a sequence of typed errors.
            /// </summary>
            public static Outcome<T, TError> FromErrors(IEnumerable<TError> errors)
            {
                return Outcome<T, TError>.FromErrors(errors.ToList());
            }
        }
    }
}
