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
        extension<T, TError>(Task<Outcome<T, TError>> task)
        {
            /// <summary>
            /// Asynchronously matches over the outcome produced by the task.
            /// Awaits the task and then applies the appropriate callback based on success or error.
            /// </summary>
            /// <typeparam name="TResult">The result type returned by the callbacks.</typeparam>
            /// <param name="onSuccess">Callback invoked with the successful value. Must not be null.</param>
            /// <param name="onError">Callback invoked with the error list when outcome is a failure. Must not be null.</param>
            /// <returns>A task that resolves to the result from either the success or error branch.</returns>
            public async Task<TResult> MatchAsync<TResult>(
                Func<T, TResult> onSuccess,
                Func<IReadOnlyList<TError>, TResult> onError)
            {
                var outcome = await task.ConfigureAwait(false);
                return outcome.Match(onSuccess, onError);
            }

            /// <summary>
            /// Asynchronously executes one of two actions depending on whether the awaited outcome is success or error.
            /// </summary>
            /// <param name="onSuccess">Callback invoked with the successful value. Must not be null.</param>
            /// <param name="onError">Callback invoked with the error list when outcome is a failure. Must not be null.</param>
            /// <returns>A task that resolves to the result from either the success or error branch.</returns>
            public async Task SwitchAsync(
                Action<T> onSuccess,
                Action<IReadOnlyList<TError>> onError)
            {
                var outcome = await task.ConfigureAwait(false);
                outcome.Switch(onSuccess, onError);
            }

            /// <summary>
            /// Asynchronously binds the underlying Outcome<T> to the specified asynchronous binder and returns the
            /// resulting Outcome<TResult>.
            /// </summary>
            /// <remarks>If the awaited outcome is a failure, the binder is not invoked and the
            /// failure is returned. The implementation uses ConfigureAwait(false) to avoid capturing the
            /// synchronization context.</remarks>
            /// <typeparam name="TResult">The type of the value contained in the resulting Outcome.</typeparam>
            /// <param name="binder">A function that receives a value of type T and returns a Task producing an Outcome<TResult>.</param>
            /// <returns>A Task that yields an Outcome<TResult, TError> produced by applying the binder to the successful result, or the
            /// original failure if the outcome is unsuccessful.</returns>
            public async Task<Outcome<TResult, TError>> BindAsync<TResult>(Func<T, Task<Outcome<TResult, TError>>> binder)
            {
                var outcome = await task.ConfigureAwait(false);
                return await outcome.BindAsync(binder).ConfigureAwait(false);
            }

            /// <summary>
            /// Asynchronously maps the successful result of the underlying Outcome<T> to an Outcome<TResult> using the
            /// provided asynchronous mapping function.
            /// </summary>
            /// <remarks>Awaits the underlying Task<Outcome<T>> before applying the mapper.</remarks>
            /// <typeparam name="TResult">The type of the mapped result.</typeparam>
            /// <param name="mapper">An asynchronous function that maps a successful value of type T to a value of type TResult.</param>
            /// <returns>A task that yields an Outcome<TResult, TError> containing the mapped value on success, or the original failure.</returns>
            public async Task<Outcome<TResult, TError>> MapAsync<TResult>(Func<T, Task<TResult>> mapper)
            {
                var outcome = await task.ConfigureAwait(false);
                return await outcome.MapAsync(mapper).ConfigureAwait(false);
            }

            /// <summary>
            /// Asynchronously combines the current task with the specified Outcome<T> tasks and returns a single
            /// Outcome containing their results.
            /// </summary>
            /// <remarks>The current instance's task is included implicitly as the first element of
            /// the combined operation.</remarks>
            /// <param name="tasks">Additional Outcome<T> tasks to combine with the current task.</param>
            /// <returns>An Outcome<IEnumerable<T, TError>> containing the results of all tasks if all succeed; otherwise an Outcome
            /// representing the aggregated failure(s).</returns>
            public async Task<Outcome<IEnumerable<T>, TError>> CombineAsync(params Task<Outcome<T, TError>>[] tasks)
            {
                var allTasks = new Task<Outcome<T, TError>>[tasks.Length + 1];
                allTasks[0] = task;
                Array.Copy(tasks, 0, allTasks, 1, tasks.Length);
                return await Outcome<T, TError>.CombineAsync(allTasks).ConfigureAwait(false);
            }
        }
    }
}
