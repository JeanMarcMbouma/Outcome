namespace BbQ.Outcome
{
    /// <summary>
    /// Static convenience extensions for constructing <see cref="Error{TCode}"/> instances
    /// with specific severity levels using a fluent builder pattern.
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
}
