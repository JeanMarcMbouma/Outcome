using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BbQ.Outcome
{
    /// <summary>
    /// Represents a structured error with a code, description, and severity level.
    /// </summary>
    /// <typeparam name="TCode">The type of the error code (e.g., enum, string, int).</typeparam>
    /// <param name="Code">The error code used to identify the error type.</param>
    /// <param name="Description">A human-readable description of what went wrong.</param>
    /// <param name="Severity">
    /// The severity level of this error. Defaults to <see cref="ErrorSeverity.Error"/>.
    /// </param>
    public sealed record Error<TCode>(TCode Code, string Description, ErrorSeverity Severity = ErrorSeverity.Error)
    {
        
        /// <summary>
        /// Converts this error to an <see cref="Outcome{T}"/> with the specified type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public Outcome<T> ToOutcome<T>()
        {
            return Outcome<T>.FromError(new Error<TCode>(Code, Description, Severity));
        }
    }
}
