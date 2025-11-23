namespace Outcome
{
    /// <summary>
    /// Defines the severity level of an error in the outcome system.
    /// Used to categorize errors by their impact and importance.
    /// </summary>
    public enum ErrorSeverity
    {
        /// <summary>
        /// Informational message; does not indicate a failure.
        /// </summary>
        Info,

        /// <summary>
        /// Validation failure; the operation did not meet required conditions.
        /// </summary>
        Validation,

        /// <summary>
        /// Warning; the operation may have succeeded but with unexpected side effects.
        /// </summary>
        Warning,

        /// <summary>
        /// Standard error; the operation failed and the error should be handled.
        /// </summary>
        Error,

        /// <summary>
        /// Critical error; the system may be in an inconsistent state.
        /// </summary>
        Critical
    }
}
