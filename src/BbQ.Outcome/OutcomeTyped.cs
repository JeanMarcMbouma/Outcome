using System.Runtime.CompilerServices;

namespace BbQ.Outcome
{
    /// <summary>
    /// A discriminated union (result type) that represents either a successful value of type <typeparamref name="T"/>
    /// or a list of strongly-typed errors of type <typeparamref name="TError"/>.
    /// Unlike <see cref="Outcome{T}"/> which stores errors as <c>object?</c>, this variant avoids boxing
    /// by preserving the error type throughout the pipeline.
    /// </summary>
    /// <typeparam name="T">The type of the successful value.</typeparam>
    /// <typeparam name="TError">The type of each error in the error list.</typeparam>
    public readonly struct Outcome<T, TError> : IOutcome<T, TError>
    {
        private readonly T? _value;
        private readonly IReadOnlyList<TError> _errors;

        /// <summary>
        /// Gets a value indicating whether this outcome represents a successful operation.
        /// When true, <see cref="Value"/> can be accessed safely.
        /// When false, <see cref="Errors"/> can be accessed safely.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets a value indicating whether this outcome represents a failed operation.
        /// This is the logical inverse of <see cref="IsSuccess"/>.
        /// </summary>
        public bool IsError => !IsSuccess;

        /// <summary>
        /// Gets the successful value. Only accessible when <see cref="IsSuccess"/> is true.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when attempting to access the value of a failure outcome.</exception>
        public T Value
        {
            get
            {
                if (!IsSuccess)
                    throw new InvalidOperationException("Cannot access Value when Outcome is a failure.");
                return _value!;
            }
        }

        /// <summary>
        /// Gets the strongly-typed list of errors. Only accessible when <see cref="IsSuccess"/> is false.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when attempting to access errors of a success outcome.</exception>
        public IReadOnlyList<TError> Errors
        {
            get
            {
                if (IsSuccess)
                    throw new InvalidOperationException("Cannot access Errors when Outcome is a success.");
                return _errors;
            }
        }

        /// <summary>
        /// Gets the successful value without validation checks.
        /// Only safe to call after confirming <see cref="IsSuccess"/> is true.
        /// </summary>
        internal T ValueUnchecked
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _value!;
        }

        /// <summary>
        /// Gets the error list without validation checks.
        /// Only safe to call after confirming <see cref="IsSuccess"/> is false.
        /// </summary>
        internal IReadOnlyList<TError> ErrorsUnchecked
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _errors;
        }

        /// <summary>
        /// Private constructor for creating a success outcome with a value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Outcome(T value) => (_value, IsSuccess, _errors) = (value, true, Array.Empty<TError>());

        /// <summary>
        /// Private constructor for creating a failure outcome with errors.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Outcome(IReadOnlyList<TError> errors) => (_errors, _value, IsSuccess) = (errors, default, false);

        /// <summary>
        /// Creates a successful outcome containing the specified value.
        /// </summary>
        /// <param name="value">The value to wrap in a success outcome.</param>
        /// <returns>An outcome representing success with the given value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Outcome<T, TError> From(T value) => new(value);

        /// <summary>
        /// Creates a failure outcome containing the specified errors.
        /// </summary>
        /// <param name="errors">A list of errors that occurred during the operation.</param>
        /// <returns>An outcome representing failure with the given errors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Outcome<T, TError> FromErrors(IReadOnlyList<TError> errors) => new(errors);

        /// <summary>
        /// Creates a failure outcome containing a single error.
        /// </summary>
        /// <param name="error">The error that occurred.</param>
        /// <returns>An outcome representing failure with the given error.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Outcome<T, TError> FromError(TError error) => new([error]);

        /// <summary>
        /// Implicitly converts a value of type <typeparamref name="T"/> to a success <see cref="Outcome{T, TError}"/>.
        /// Enables ergonomic return statements like <c>return 42;</c>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Outcome<T, TError>(T value) => new(value);

        /// <summary>
        /// Returns a human-readable string representation of the outcome.
        /// </summary>
        public override string ToString()
        {
            return IsSuccess ? $"Success: {Value}" : $"Error: [{string.Join(", ", Errors)}]";
        }

        /// <summary>
        /// Deconstructs the outcome into three components: success flag, value, and errors.
        /// </summary>
        public void Deconstruct(out bool isSuccess, out T? value, out IReadOnlyList<TError>? errors)
        {
            isSuccess = IsSuccess;
            value = IsSuccess ? _value : default;
            errors = IsSuccess ? null : _errors;
        }

        /// <summary>
        /// Deconstructs the outcome into two components: value and errors.
        /// </summary>
        public void Deconstruct(out T? value, out IReadOnlyList<TError>? errors)
        {
            value = _value;
            errors = _errors;
        }
    }
}
