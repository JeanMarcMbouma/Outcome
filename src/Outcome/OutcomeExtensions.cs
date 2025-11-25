using BbQ.Outcome;

namespace BbQ.Outcome
{
    /// <summary>
    /// Extension methods that provide functional composition patterns for <see cref="Outcome{T}"/> values.
    /// These methods implement common functional programming patterns:
    /// - Match/Switch: Pattern matching and side-effect execution
    /// - Bind/Map: Monadic and functor operations
    /// - Combine: Aggregating multiple outcomes
    /// - Async variants (MapAsync, BindAsync, CombineAsync) for async workflows
    /// - Error constructors: Convenience methods for creating error outcomes
    /// - Strongly-typed error access: Methods to retrieve errors as specific types
    /// </summary>
    public static class OutcomeExtensions
    {
        // C# 14 extension type for instance methods on Outcome<T>
        extension<T>(Outcome<T> outcome)
        {
            /// <summary>
            /// Pattern-matches over the outcome: if successful, invokes <paramref name="onSuccess"/>,
            /// otherwise invokes <paramref name="onError"/> with the list of errors.
            /// Returns the result produced by the invoked delegate.
            /// </summary>
            /// <typeparam name="TResult">The result type returned by the chosen branch.</typeparam>
            /// <param name="onSuccess">Callback invoked with the successful value. Must not be null.</param>
            /// <param name="onError">Callback invoked with the error list when outcome is a failure. Must not be null.</param>
            /// <returns>The result from either the success or error branch.</returns>
            /// <example>
            /// <code>
            /// var result = outcome.Match(
            ///     onSuccess: value => $"Got {value}",
            ///     onError: errors => $"Errors: {string.Join("; ", errors)}"
            /// );
            /// </code>
            /// </example>
            public TResult Match<TResult>(
                Func<T, TResult> onSuccess,
                Func<IReadOnlyList<object?>, TResult> onError)
            {
                // If the outcome carries a value, produce the success branch result,
                // otherwise produce the error branch result using the stored errors.
                return outcome.IsSuccess
                    ? onSuccess(outcome.Value)
                    : onError(outcome.Errors);
            }

            /// <summary>
            /// Executes one of two actions depending on whether the outcome is success or error.
            /// Use this method when you only need side-effects and no return value.
            /// Similar to Match but returns void.
            /// </summary>
            /// <param name="onSuccess">Action executed with the successful value. Must not be null.</param>
            /// <param name="onError">Action executed with the error list when outcome is a failure. Must not be null.</param>
            /// <example>
            /// <code>
            /// outcome.Switch(
            ///     onSuccess: value => Console.WriteLine($"Success: {value}"),
            ///     onError: errors => Console.WriteLine($"Failed with {errors.Count} errors")
            /// );
            /// </code>
            /// </example>
            public void Switch(
                Action<T> onSuccess,
                Action<IReadOnlyList<object?>> onError)
            {
                if (outcome.IsSuccess)
                    onSuccess(outcome.Value);
                else
                    onError(outcome.Errors);
            }

            /// <summary>
            /// Monadic bind operation (also known as flatMap or chain).
            /// When the outcome is successful, applies <paramref name="binder"/> to the contained value
            /// to produce a new <see cref="Outcome{TResult}"/>. If the current outcome is an error,
            /// propagates the existing errors to the resulting outcome without invoking the binder.
            /// </summary>
            /// <typeparam name="TResult">The type of the result produced by the binder.</typeparam>
            /// <param name="binder">A function that transforms the successful value into a new outcome. Must not be null.</param>
            /// <returns>
            /// A new outcome that is either the result of applying the binder (if successful),
            /// or a failure containing the original errors.
            /// </returns>
            /// <example>
            /// <code>
            /// var result = ParseInt("42")
            ///     .Bind(x => x > 0 ? Outcome<int>.From(x * 2) : Outcome<int>.Validation("NEG", "Must be positive"));
            /// </code>
            /// </example>
            public Outcome<TResult> Bind<TResult>(Func<T, Outcome<TResult>> binder)
            {
                return outcome.IsSuccess
                    ? binder(outcome.Value)
                    : Outcome<TResult>.FromErrors(outcome.Errors!);
            }

            /// <summary>
            /// Functor map operation. Transforms the successful value using <paramref name="mapper"/>
            /// while preserving any errors. This is the standard map/select operation for the Outcome type.
            /// </summary>
            /// <typeparam name="TResult">The type of the result produced by the mapper.</typeparam>
            /// <param name="mapper">A function that transforms the successful value. Must not be null.</param>
            /// <returns>
            /// A new outcome containing the transformed value (if successful),
            /// or the same errors (if a failure).
            /// </returns>
            /// <example>
            /// <code>
            /// var doubled = ParseInt("21")
            ///     .Map(x => x * 2);  // If success, value becomes 42; if error, error propagates.
            /// </code>
            /// </example>
            public Outcome<TResult> Map<TResult>(Func<T, TResult> mapper)
            {
                return outcome.IsSuccess
                    ? Outcome<TResult>.From(mapper(outcome.Value))
                    : Outcome<TResult>.FromErrors(outcome.Errors!);
            }

            /// <summary>
            /// Combines multiple outcomes into a single outcome containing an enumerable of values.
            /// When all inputs are successful, returns a success outcome with all their values.
            /// If any input is an error, aggregates and returns all errors in a failure outcome.
            /// </summary>
            /// <param name="outcomes">A set of outcomes to combine. Must not be null.</param>
            /// <returns>
            /// A success outcome containing an enumerable of all values (if all inputs are successful),
            /// or a failure outcome containing the aggregated errors.
            /// </returns>
            /// <example>
            /// <code>
            /// var combined = Outcome<int>.Combine(ParseInt("1"), ParseInt("2"), ParseInt("3"));
            /// // If all succeed: Success with IEnumerable<int> { 1, 2, 3 }
            /// // If any fail: Failure with aggregated errors
            /// </code>
            /// </example>
            public static Outcome<IEnumerable<T>> Combine(params IEnumerable<Outcome<T>> outcomes)
            {
                // Collect errors from all provided outcomes.
                var errors = outcomes
                    .Where(o => o.IsError)
                    .SelectMany(o => o.Errors);
                return errors.Any()
                    ? Outcome<IEnumerable<T>>.FromErrors(errors.ToList()!)
                    : Outcome<IEnumerable<T>>.From(outcomes.Select(r => r.Value));
            }

            // ============ Async composition methods ============

            /// <summary>
            /// Asynchronously maps the successful value using an async mapper function.
            /// If the outcome is an error, errors are preserved and the mapper is not invoked.
            /// </summary>
            /// <typeparam name="TResult">The result type produced by the async mapper.</typeparam>
            /// <param name="mapper">An async function that transforms the successful value. Must not be null.</param>
            /// <returns>
            /// A task that represents the asynchronous mapping operation.
            /// Resolves to a success outcome with the transformed value, or a failure outcome with errors.
            /// </returns>
            /// <example>
            /// <code>
            /// var result = outcome
            ///     .MapAsync(x => FetchDataAsync(x));
            /// </code>
            /// </example>
            public async Task<Outcome<TResult>> MapAsync<TResult>(Func<T, Task<TResult>> mapper) =>
                outcome.IsSuccess ? Outcome<TResult>.From(await mapper(outcome.Value)) : Outcome<TResult>.FromErrors(outcome.Errors!);

            /// <summary>
            /// Asynchronously binds (flatMaps) the successful value using an async binder that returns an Outcome.
            /// Errors are propagated when the current outcome is an error.
            /// </summary>
            /// <typeparam name="TResult">The result type of the async binder.</typeparam>
            /// <param name="binder">An async function that transforms the value into a new outcome. Must not be null.</param>
            /// <returns>
            /// A task that represents the asynchronous bind operation.
            /// Resolves to the outcome produced by the binder, or a failure outcome with errors.
            /// </returns>
            /// <example>
            /// <code>
            /// var result = ParseIntAsync("42")
            ///     .BindAsync(x => ValidateAndFetchAsync(x));
            /// </code>
            /// </example>
            public async Task<Outcome<TResult>> BindAsync<TResult>(Func<T, Task<Outcome<TResult>>> binder) =>
                outcome.IsSuccess ? await binder(outcome.Value) : Outcome<TResult>.FromErrors(outcome.Errors!);

            /// <summary>
            /// Awaits multiple outcome-producing tasks and combines their results.
            /// Behavior is similar to <see cref="Combine"/>, but operates on asynchronous tasks.
            /// If any awaited outcome is an error, returns an outcome with all aggregated errors.
            /// </summary>
            /// <param name="tasks">An array of tasks producing outcomes. Must not be null.</param>
            /// <returns>
            /// A task that represents the asynchronous combination operation.
            /// Resolves to a success outcome with an enumerable of all values (if all tasks succeed),
            /// or a failure outcome with aggregated errors.
            /// </returns>
            /// <example>
            /// <code>
            /// var combined = await Outcome<int>.CombineAsync(
            ///     FetchAsync(1),
            ///     FetchAsync(2),
            ///     FetchAsync(3)
            /// );
            /// </code>
            /// </example>
            public static async Task<Outcome<IEnumerable<T>>> CombineAsync(params Task<Outcome<T>>[] tasks)
            {
                var results = await Task.WhenAll(tasks);
                var errors = results.Where(r => r.IsError).SelectMany(r => r.Errors).ToList();
                if (errors.Count != 0)
                    return Outcome<IEnumerable<T>>.FromErrors(errors!);

                var values = results.Select(r => r.Value).ToList();
                return Outcome<IEnumerable<T>>.From(values);
            }
        }

        // C# 14 extension type for static factory methods on Outcome<T>
        extension<T>(Outcome<T>)
        {
            /// <summary>
            /// Creates an outcome representing an error with a typed error code and textual description.
            /// Default severity is <see cref="ErrorSeverity.Error"/>.
            /// </summary>
            /// <typeparam name="TCode">The type of the error code (e.g., enum, string).</typeparam>
            /// <param name="code">The error code to identify the error type.</param>
            /// <param name="description">A human-readable description of the error.</param>
            /// <param name="severity">The severity level of the error. Defaults to Error.</param>
            /// <returns>An error outcome containing the specified error.</returns>
            /// <example>
            /// <code>
            /// return Outcome<int>.FromError(ErrorCodes.DivisionByZero, "Cannot divide by zero", ErrorSeverity.Error);
            /// </code>
            /// </example>
            public static Outcome<T> FromError<TCode>(TCode code, string description, ErrorSeverity severity = ErrorSeverity.Error)
            {
                var error = new Error<TCode>(code, description, severity);
                // Wrap the single error in a collection to construct an error Outcome.
                return Outcome<T>.FromErrors([error]);
            }

            /// <summary>
            /// Creates an error outcome from a pre-constructed <see cref="Error{TCode}"/> instance.
            /// Useful when error construction is done elsewhere and you just need to wrap it.
            /// </summary>
            /// <typeparam name="TCode">The type of the error code.</typeparam>
            /// <param name="error">The error instance to wrap. Must not be null.</param>
            /// <returns>An error outcome containing the specified error.</returns>
            public static Outcome<T> FromError<TCode>(Error<TCode> error)
            {
                return Outcome<T>.FromErrors([error]);
            }

            /// <summary>
            /// Creates an error outcome from a sequence of typed errors.
            /// Useful for aggregating multiple errors into a single outcome.
            /// </summary>
            /// <typeparam name="TCode">The type of the error code.</typeparam>
            /// <param name="errors">An enumerable of errors. Must not be null.</param>
            /// <returns>An error outcome containing all the specified errors.</returns>
            public static Outcome<T> FromErrors<TCode>(IEnumerable<Error<TCode>> errors)
            {
                return Outcome<T>.FromErrors(errors.ToList());
            }

            /// <summary>
            /// Creates a validation error outcome with severity set to <see cref="ErrorSeverity.Validation"/>.
            /// Use when an operation fails validation checks.
            /// </summary>
            /// <typeparam name="TCode">The type of the error code.</typeparam>
            /// <param name="code">The error code identifying the validation failure.</param>
            /// <param name="description">A human-readable description of what failed validation.</param>
            /// <returns>A validation error outcome.</returns>
            /// <example>
            /// <code>
            /// return Outcome<int>.Validation(ErrorCodes.InvalidInput, "Input must be positive");
            /// </code>
            /// </example>
            public static Outcome<T> Validation<TCode>(TCode code, string description)
            {
                var error = new Error<TCode>(code, description, ErrorSeverity.Validation);
                return Outcome<T>.FromErrors([error]);
            }

            /// <summary>
            /// Creates a critical error outcome with severity set to <see cref="ErrorSeverity.Critical"/>.
            /// Use when the system may be in an inconsistent state.
            /// </summary>
            /// <typeparam name="TCode">The type of the error code.</typeparam>
            /// <param name="code">The error code identifying the critical error.</param>
            /// <param name="description">A human-readable description of the critical error.</param>
            /// <returns>A critical error outcome.</returns>
            public static Outcome<T> Critical<TCode>(TCode code, string description)
            {
                var error = new Error<TCode>(code, description, ErrorSeverity.Critical);
                return Outcome<T>.FromErrors([error]);
            }
        }
    }

    /// <summary>
    /// Extension methods for accessing strongly-typed errors from <see cref="Outcome{T}"/> instances.
    /// Provides convenient methods to filter and retrieve errors of a specific type.
    /// </summary>
    public static class OutcomeErrorExtensions
    {
        extension<T>(Outcome<T> outcome)
        {
            /// <summary>
            /// Gets all errors of a specific type from the outcome.
            /// Filters the error collection to return only errors matching the specified error code type.
            /// </summary>
            /// <typeparam name="T">The outcome's success value type.</typeparam>
            /// <typeparam name="TCode">The error code type to filter by.</typeparam>
            /// <param name="outcome">The outcome to extract errors from.</param>
            /// <returns>An enumerable of strongly-typed errors matching the specified code type.</returns>
            /// <example>
            /// <code>
            /// var appErrors = outcome.GetErrors&lt;Unit, AppError&gt;();
            /// foreach (var error in appErrors)
            /// {
            ///     Console.WriteLine($"Error: {error.Code} - {error.Description}");
            /// }
            /// </code>
            /// </example>
            public IEnumerable<Error<TCode>> GetErrors<TCode>()
            {
                if (outcome.IsSuccess)
                    return Enumerable.Empty<Error<TCode>>();

                return outcome.Errors.OfType<Error<TCode>>();
            }

            /// <summary>
            /// Gets the first error of a specific type from the outcome, or null if none exists.
            /// Useful for handling a single error from a known type.
            /// </summary>
            /// <typeparam name="T">The outcome's success value type.</typeparam>
            /// <typeparam name="TCode">The error code type to retrieve.</typeparam>
            /// <param name="outcome">The outcome to extract an error from.</param>
            /// <returns>The first strongly-typed error, or null if no errors of that type exist.</returns>
            /// <example>
            /// <code>
            /// var error = outcome.GetError&lt;int, AppError&gt;();
            /// if (error != null)
            /// {
            ///     Console.WriteLine($"Error: {error.Code} - {error.Description}");
            /// }
            /// </code>
            /// </example>
            public Error<TCode>? GetError<TCode>()
            {
                return outcome.GetErrors<T, TCode>().FirstOrDefault();
            }

            /// <summary>
            /// Checks if the outcome contains any errors of a specific type.
            /// </summary>
            /// <typeparam name="T">The outcome's success value type.</typeparam>
            /// <typeparam name="TCode">The error code type to check for.</typeparam>
            /// <param name="outcome">The outcome to check.</param>
            /// <returns>True if the outcome contains at least one error of the specified type; otherwise, false.</returns>
            /// <example>
            /// <code>
            /// if (outcome.HasErrors&lt;Unit, AppError&gt;())
            /// {
            ///     var errors = outcome.GetErrors&lt;Unit, AppError&gt;();
            ///     // Handle errors
            /// }
            /// </code>
            /// </example>
            public bool HasErrors<TCode>()
            {
                return outcome.GetErrors<T, TCode>().Any();
            }

            /// <summary>
            /// Gets errors of a specific type that match a predicate condition.
            /// Useful for filtering errors by code or other properties.
            /// </summary>
            /// <typeparam name="T">The outcome's success value type.</typeparam>
            /// <typeparam name="TCode">The error code type to filter.</typeparam>
            /// <param name="outcome">The outcome to extract errors from.</param>
            /// <param name="predicate">A function to filter errors by.</param>
            /// <returns>An enumerable of strongly-typed errors matching the predicate.</returns>
            /// <example>
            /// <code>
            /// var validationErrors = outcome.GetErrors&lt;int, AppError&gt;(
            ///     e => e.Severity == ErrorSeverity.Validation
            /// );
            /// </code>
            /// </example>
            public IEnumerable<Error<TCode>> GetErrors<TCode>(
                Func<Error<TCode>, bool> predicate)
            {
                return outcome.GetErrors<T, TCode>().Where(predicate);
            }
        }
    }
}
