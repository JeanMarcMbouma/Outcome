namespace BbQ.Outcome
{
    /// <summary>
    /// Static convenience extensions for constructing and converting <see cref="Error{TCode}"/> instances.
    /// Provides factory methods for errors with specific severity levels and conversion methods to outcomes.
    /// </summary>
    public static class ErrorExtensions
    {
        // C# 14 extension type for Error<TCode> — provides static factory methods for error construction
        extension<TCode>(Error<TCode>)
        {
            /// <summary>
            /// Creates an informational error with <see cref="ErrorSeverity.Info"/> severity.
            /// Use for non-critical messages that don't indicate failure.
            /// </summary>
            /// <param name="code">The error code to identify the type of information.</param>
            /// <param name="description">A human-readable description of the information.</param>
            /// <returns>An Error record with Info severity.</returns>
            public static Error<TCode> Info(TCode code, string description)
            {
                return new Error<TCode>(code, description, ErrorSeverity.Info);
            }

            /// <summary>
            /// Creates a validation error with <see cref="ErrorSeverity.Validation"/> severity.
            /// Use when an operation fails validation checks or preconditions.
            /// </summary>
            /// <param name="code">The error code identifying the validation failure.</param>
            /// <param name="description">A human-readable description of what failed validation.</param>
            /// <returns>An Error record with Validation severity.</returns>
            public static Error<TCode> Validation(TCode code, string description)
            {
                return new Error<TCode>(code, description, ErrorSeverity.Validation);
            }

            /// <summary>
            /// Creates a warning error with <see cref="ErrorSeverity.Warning"/> severity.
            /// Use when the operation succeeded but there are potential issues to note.
            /// </summary>
            /// <param name="code">The error code identifying the warning.</param>
            /// <param name="description">A human-readable description of the warning.</param>
            /// <returns>An Error record with Warning severity.</returns>
            public static Error<TCode> Warning(TCode code, string description)
            {
                return new Error<TCode>(code, description, ErrorSeverity.Warning);
            }

            /// <summary>
            /// Creates a critical error with <see cref="ErrorSeverity.Critical"/> severity.
            /// Use when the system may be in an inconsistent state or a critical operation failed.
            /// </summary>
            /// <param name="code">The error code identifying the critical error.</param>
            /// <param name="description">A human-readable description of the critical error.</param>
            /// <returns>An Error record with Critical severity.</returns>
            public static Error<TCode> Critical(TCode code, string description)
            {
                return new Error<TCode>(code, description, ErrorSeverity.Critical);
            }
        }
    }

    /// <summary>
    /// Extension methods for converting <see cref="Error{TCode}"/> instances to <see cref="Outcome{T}"/>.
    /// </summary>
    public static class ErrorConversionExtensions
    {
        /// <summary>
        /// Converts this error to an <see cref="Outcome{T}"/> with the specified type.
        /// Provides an ergonomic way to return errors from methods that return <see cref="Outcome{T}"/>.
        /// </summary>
        /// <typeparam name="TCode">The type of the error code.</typeparam>
        /// <typeparam name="T">The success value type of the target outcome.</typeparam>
        /// <param name="error">The error to convert.</param>
        /// <returns>An error outcome containing this error.</returns>
        /// <example>
        /// <code>
        /// public Outcome&lt;int&gt; Divide(int a, int b)
        /// {
        ///     if (b == 0)
        ///         return new Error&lt;DivisionError&gt;(DivisionError.DivideByZero, "Cannot divide by zero").AsOutcome&lt;int&gt;();
        ///     return a / b;
        /// }
        /// </code>
        /// </example>
        public static Outcome<T> AsOutcome<TCode, T>(this Error<TCode> error)
        {
            return Outcome<T>.FromError(error);
        }
    }
}
