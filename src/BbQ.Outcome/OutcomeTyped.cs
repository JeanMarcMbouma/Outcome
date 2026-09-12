using System.Runtime.CompilerServices;

namespace BbQ.Outcome
{
    /// <summary>
    /// Either a successful value or a nonempty, structurally immutable list of typed errors.
    /// </summary>
    /// <remarks>
    /// Construct instances with From, FromError, or FromErrors. A default instance is
    /// uninitialized: its status is unsuccessful, but consuming its errors or deconstructing
    /// it throws InvalidOperationException. Error objects themselves are not deep-cloned.
    /// </remarks>
    /// <typeparam name="T">The successful value type.</typeparam>
    /// <typeparam name="TError">The error type; no interface constraint is required.</typeparam>
    public readonly struct Outcome<T, TError> : IOutcome<T, TError>
    {
        private readonly T? _value;
        private readonly IReadOnlyList<TError>? _errors;

        /// <summary>True only for an explicitly constructed success.</summary>
        public bool IsSuccess { get; }

        /// <summary>The inverse of IsSuccess; does not validate initialization.</summary>
        public bool IsError => !IsSuccess;

        /// <summary>Gets the value; throws on failure or an uninitialized outcome.</summary>
        public T Value
        {
            get
            {
                if (!IsSuccess)
                    throw new InvalidOperationException("Cannot access Value when Outcome is a failure.");
                return _value!;
            }
        }

        /// <summary>Gets the errors; throws on success or an uninitialized outcome.</summary>
        public IReadOnlyList<TError> Errors
        {
            get
            {
                if (IsSuccess)
                    throw new InvalidOperationException("Cannot access Errors when Outcome is a success.");
                return ErrorsUnchecked;
            }
        }

        internal T ValueUnchecked
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _value!;
        }

        // The branch has already been checked, but default structs still need validation.
        internal IReadOnlyList<TError> ErrorsUnchecked
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _errors ?? throw new InvalidOperationException(
                "Outcome is uninitialized. Use From, FromError, or FromErrors instead of default.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Outcome(T value) => (_value, IsSuccess, _errors) = (value, true, Array.Empty<TError>());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Outcome(ErrorCollection<TError> errors) => (_errors, _value, IsSuccess) = (errors, default, false);

        /// <summary>Creates a success. A null success value is permitted.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Outcome<T, TError> From(T value) => new(value);

        /// <summary>
        /// Creates a failure by snapshotting a nonempty list of non-null errors.
        /// Library-owned immutable snapshots are reused when propagating a failure.
        /// </summary>
        /// <exception cref="ArgumentNullException">The list is null.</exception>
        /// <exception cref="ArgumentException">The list is empty or contains a null error.</exception>
        public static Outcome<T, TError> FromErrors(IReadOnlyList<TError> errors)
            => new(ErrorCollection<TError>.Snapshot(errors));

        /// <summary>Creates a failure from a single non-null error.</summary>
        public static Outcome<T, TError> FromError(TError error)
            => new(ErrorCollection<TError>.Single(error));

        /// <summary>Implicitly converts a value to a success.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Outcome<T, TError>(T value) => From(value);

        /// <summary>Implicitly converts a non-null error to a failure.</summary>
        public static implicit operator Outcome<T, TError>(TError error) => FromError(error);

        /// <summary>Returns a readable representation; an uninitialized outcome is invalid.</summary>
        public override string ToString()
            => IsSuccess ? $"Success: {Value}" : $"Error: [{string.Join(", ", Errors)}]";

        /// <summary>Deconstructs into status, value, and errors (null on success).</summary>
        public void Deconstruct(out bool isSuccess, out T? value, out IReadOnlyList<TError>? errors)
        {
            isSuccess = IsSuccess;
            value = IsSuccess ? _value : default;
            errors = IsSuccess ? null : ErrorsUnchecked;
        }

        /// <summary>Deconstructs into value and errors (an empty list on success).</summary>
        public void Deconstruct(out T? value, out IReadOnlyList<TError>? errors)
        {
            value = _value;
            errors = ErrorsUnchecked;
        }
    }
}
