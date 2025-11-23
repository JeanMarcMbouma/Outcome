namespace BbQ.Outcome
{
    /// <summary>
    /// A discriminated union (result type) that represents either a successful value of type <typeparam name="T"/>
    /// or a list of errors. This is the core abstraction for functional error handling in Outcome.
    /// 
    /// Use <see cref="From(T)"/> to construct a success outcome, or <see cref="FromErrors(IReadOnlyList{object})"/>
    /// to construct a failure outcome. The <see cref="IsSuccess"/> property indicates which case you're in.
    /// </summary>
    /// <typeparam name="T">The type of the successful value.</typeparam>
    public readonly struct Outcome<T>
    {
        private readonly T? _value;
        private readonly IReadOnlyList<object?> _errors;

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
                // Prevent accidental access to the value when the outcome is a failure
                if (!IsSuccess)
                    throw new InvalidOperationException("Cannot access Value when Outcome is a failure.");
                return _value!;
            }
        }

        /// <summary>
        /// Gets the list of errors. Only accessible when <see cref="IsSuccess"/> is false.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when attempting to access errors of a success outcome.</exception>
        public IReadOnlyList<object?> Errors
        {
            get
            {
                // Prevent accidental access to errors when the outcome is a success
                if (IsSuccess)
                    throw new InvalidOperationException("Cannot access Errors when Outcome is a success.");
                return _errors;
            }
        }

        /// <summary>
        /// Private constructor for creating a success outcome with a value.
        /// </summary>
        private Outcome(T value) => (_value, IsSuccess, _errors) = (value, true, []);

        /// <summary>
        /// Private constructor for creating a failure outcome with errors.
        /// </summary>
        private Outcome(IReadOnlyList<object?> errors) => (_errors, _value, IsSuccess) = (errors, default, false);

        /// <summary>
        /// Creates a successful outcome containing the specified value.
        /// </summary>
        /// <param name="value">The value to wrap in a success outcome.</param>
        /// <returns>An outcome representing success with the given value.</returns>
        public static Outcome<T> From(T value) => new(value);

        /// <summary>
        /// Creates a failure outcome containing the specified errors.
        /// </summary>
        /// <param name="errors">A list of errors that occurred during the operation.</param>
        /// <returns>An outcome representing failure with the given errors.</returns>
        public static Outcome<T> FromErrors(IReadOnlyList<object> errors) => new(errors);

        /// <summary>
        /// Implicitly converts a value of type <typeparam name="T"/> to an <see cref="Outcome{T}"/> success.
        /// Enables ergonomic return statements like <c>return 42;</c> in a method returning <see cref="Outcome{T}"/>.
        /// </summary>
        public static implicit operator Outcome<T>(T value) => new(value);

        /// <summary>
        /// Returns a human-readable string representation of the outcome.
        /// Success outcomes display "Success: {value}"; failure outcomes display "Error: [error1, error2, ...]".
        /// </summary>
        public override string ToString()
        {
            return IsSuccess ? $"Success: {Value}" : $"Error: [{string.Join(", ", Errors)}]";
        }

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
            isSuccess = IsSuccess;
            value = IsSuccess ? _value : default;
            errors = IsSuccess ? null : _errors;
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
            value = _value;
            errors = _errors;
        }
    }
}
