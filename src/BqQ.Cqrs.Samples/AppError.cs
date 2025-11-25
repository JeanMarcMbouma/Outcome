using BbQ.Outcome;

namespace BbQ.CQRS.Samples;

// Example source-generated errors (enum-based)
// Assume your Outcome generators produce a strongly typed Error model usable here.
[QbqOutcome]
public enum AppError
{
    UserNotFound,
    InvalidName,
    Unauthorized,
    Conflict,
    /// <summary>
    /// A transient error that may succeed if retried
    /// </summary>
    [ErrorSeverity(ErrorSeverity.Error)]
    Transient
}
