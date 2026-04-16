using System.Runtime.CompilerServices;

namespace BbQ.Outcome
{
    /// <summary>
    /// A discriminated union (result type) that represents either a successful value of type <typeparamref name="T"/>
    /// or a list of errors. This is a thin wrapper over <see cref="Outcome{T, TError}">Outcome&lt;T, object?&gt;</see>
    /// that stores errors as <see cref="IReadOnlyList{T}">IReadOnlyList&lt;object?&gt;</see>, supporting heterogeneous error types.
    /// 
    /// For a strongly-typed variant that avoids boxing, use <see cref="Outcome{T, TError}"/> directly.
    /// 
    /// Use <see cref="From(T)"/> to construct a success outcome, or <see cref="FromErrors(IReadOnlyList{object})"/>
    /// to construct a failure outcome. The <see cref="IsSuccess"/> property indicates which case you're in.
    /// </summary>
    /// <typeparam name="T">The type of the successful value.</typeparam>
    public readonly struct Outcome<T> : IOutcome<T>
    {
        private readonly Outcome<T, object?> _inner;

        /// <summary>
        /// Gets a value indicating whether this outcome represents a successful operation.
        /// When true, <see cref="Value"/> can be accessed safely.
        /// When false, <see cref="Errors"/> can be accessed safely.
        /// </summary>
        public bool IsSuccess => _inner.IsSuccess;

        /// <summary>
        /// Gets a value indicating whether this outcome represents a failed operation.
        /// This is the logical inverse of <see cref="IsSuccess"/>.
        /// </summary>
        public bool IsError => _inner.IsError;

        /// <summary>
        /// Gets the successful value. Only accessible when <see cref="IsSuccess"/> is true.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when attempting to access the value of a failure outcome.</exception>
        public T Value => _inner.Value;

        /// <summary>
        /// Gets the list of errors. Only accessible when <see cref="IsSuccess"/> is false.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when attempting to access errors of a success outcome.</exception>
        public IReadOnlyList<object?> Errors => _inner.Errors;

        /// <summary>
        /// Private constructor wrapping the inner typed outcome.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Outcome(Outcome<T, object?> inner) => _inner = inner;

        /// <summary>
        /// Gets the successful value without validation checks.
        /// Only safe to call after confirming <see cref="IsSuccess"/> is true.
        /// </summary>
        internal T ValueUnchecked
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _inner.ValueUnchecked;
        }

        /// <summary>
        /// Gets the error list without validation checks.
        /// Only safe to call after confirming <see cref="IsSuccess"/> is false.
        /// </summary>
        internal IReadOnlyList<object?> ErrorsUnchecked
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _inner.ErrorsUnchecked;
        }

        /// <summary>
        /// Creates a successful outcome containing the specified value.
        /// </summary>
        /// <param name="value">The value to wrap in a success outcome.</param>
        /// <returns>An outcome representing success with the given value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Outcome<T> From(T value) => new(Outcome<T, object?>.From(value));

        /// <summary>
        /// Creates a failure outcome containing the specified errors.
        /// </summary>
        /// <param name="errors">A list of errors that occurred during the operation.</param>
        /// <returns>An outcome representing failure with the given errors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Outcome<T> FromErrors(IReadOnlyList<object?> errors) => new(Outcome<T, object?>.FromErrors(errors));

        /// <summary>
        /// Implicitly converts a value of type <typeparamref name="T"/> to an <see cref="Outcome{T}"/> success.
        /// Enables ergonomic return statements like <c>return 42;</c> in a method returning <see cref="Outcome{T}"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Outcome<T>(T value) => new(Outcome<T, object?>.From(value));

        /// <summary>
        /// Gets all errors of a specific type from the outcome.
        /// Filters the error collection to return only errors matching the specified error code type.
        /// </summary>
        /// <typeparam name="TCode">The error code type to filter by.</typeparam>
        /// <returns>An enumerable of strongly-typed errors matching the specified code type.</returns>
        /// <example>
        /// <code>
        /// var appErrors = outcome.GetErrors&lt;AppError&gt;();
        /// foreach (var error in appErrors)
        /// {
        ///     Console.WriteLine($"Error: {error.Code} - {error.Description}");
        /// }
        /// </code>
        /// </example>
        public IEnumerable<Error<TCode>> GetErrors<TCode>()
        {
            if (IsSuccess)
                return Array.Empty<Error<TCode>>();

            return EnumerateTypedErrors<TCode>(_inner.ErrorsUnchecked);
        }

        private static IEnumerable<Error<TCode>> EnumerateTypedErrors<TCode>(IReadOnlyList<object?> errors)
        {
            for (var i = 0; i < errors.Count; i++)
            {
                if (errors[i] is Error<TCode> typedError)
                {
                    yield return typedError;
                }
            }
        }

        /// <summary>
        /// Gets the first error of a specific type from the outcome, or null if none exists.
        /// Useful for handling a single error from a known type.
        /// </summary>
        /// <typeparam name="TCode">The error code type to retrieve.</typeparam>
        /// <returns>The first strongly-typed error, or null if no errors of that type exist.</returns>
        /// <example>
        /// <code>
        /// var error = outcome.GetError&lt;AppError&gt;();
        /// if (error != null)
        /// {
        ///     Console.WriteLine($"Error: {error.Code} - {error.Description}");
        /// }
        /// </code>
        /// </example>
        public Error<TCode>? GetError<TCode>()
        {
            if (IsSuccess)
                return null;

            var errors = _inner.ErrorsUnchecked;
            for (var i = 0; i < errors.Count; i++)
            {
                if (errors[i] is Error<TCode> typedError)
                    return typedError;
            }

            return null;
        }

        /// <summary>
        /// Checks if the outcome contains any errors of a specific type.
        /// </summary>
        /// <typeparam name="TCode">The error code type to check for.</typeparam>
        /// <returns>True if the outcome contains at least one error of the specified type; otherwise, false.</returns>
        /// <example>
        /// <code>
        /// if (outcome.HasErrors&lt;AppError&gt;())
        /// {
        ///     var errors = outcome.GetErrors&lt;AppError&gt;();
        ///     // Handle errors
        /// }
        /// </code>
        /// </example>
        public bool HasErrors<TCode>()
        {
            if (IsSuccess)
                return false;

            var errors = _inner.ErrorsUnchecked;
            for (var i = 0; i < errors.Count; i++)
            {
                if (errors[i] is Error<TCode>)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Gets errors of a specific type that match a predicate condition.
        /// Useful for filtering errors by code or other properties.
        /// </summary>
        /// <typeparam name="TCode">The error code type to filter.</typeparam>
        /// <param name="predicate">A function to filter errors by.</param>
        /// <returns>An enumerable of strongly-typed errors matching the predicate.</returns>
        /// <example>
        /// <code>
        /// var validationErrors = outcome.GetErrors&lt;AppError&gt;(
        ///     e => e.Severity == ErrorSeverity.Validation
        /// );
        /// </code>
        /// </example>
        public IEnumerable<Error<TCode>> GetErrors<TCode>(
            Func<Error<TCode>, bool> predicate)
        {
            if (IsSuccess)
                return Array.Empty<Error<TCode>>();

            return EnumerateTypedErrors(_inner.ErrorsUnchecked, predicate);

            static IEnumerable<Error<TCode>> EnumerateTypedErrors(
                IReadOnlyList<object?> errors,
                Func<Error<TCode>, bool> filter)
            {
                for (var i = 0; i < errors.Count; i++)
                {
                    if (errors[i] is Error<TCode> typedError && filter(typedError))
                    {
                        yield return typedError;
                    }
                }
            }
        }

        /// <summary>
        /// Returns a human-readable string representation of the outcome.
        /// Success outcomes display "Success: {value}"; failure outcomes display "Error: [error1, error2, ...]".
        /// </summary>
        public override string ToString() => _inner.ToString();

        /// <summary>
        /// Deconstructs the outcome into three components: success flag, value, and errors.
        /// When the outcome is successful, value is the contained value and errors is null.
        /// When the outcome is a failure, errors contains the error list and value is default(T).
        /// </summary>
        /// <example>
        /// <code>
        /// var (isSuccess, value, errors) = outcome;
        /// if (isSuccess) { /* use value */ } else { /* use errors */ }
        /// </code>
        /// </example>
        public void Deconstruct(out bool isSuccess, out T? value, out IReadOnlyList<object?>? errors)
        {
            _inner.Deconstruct(out isSuccess, out value, out errors);
        }

        /// <summary>
        /// Deconstructs the outcome into two components: value and errors.
        /// This overload is handy when you don't care about the IsSuccess flag and want direct access to both.
        /// </summary>
        /// <example>
        /// <code>
        /// var (value, errors) = outcome;
        /// // Use value when success; use errors when failure.
        /// </code>
        /// </example>
        public void Deconstruct(out T? value, out IReadOnlyList<object?>? errors)
        {
            _inner.Deconstruct(out value, out errors);
        }
    }
}
