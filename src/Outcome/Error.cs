namespace Outcome
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
    public sealed record Error<TCode>(TCode Code, string Description, ErrorSeverity Severity = ErrorSeverity.Error);
}
