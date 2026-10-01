namespace BbQ.Outcome;

/// <summary>Configures explicit retries for Outcome-producing operations.</summary>
/// <remarks>
/// <see cref="MaxAttempts"/> includes the initial invocation. A value of 3 means at most
/// three operation calls: the initial attempt followed by up to two retries.
/// </remarks>
public sealed class RetryOptions
{
    /// <summary>Total number of operation attempts, including the initial invocation.</summary>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>
    /// Optional delay factory invoked before a retry. The argument is the retry number,
    /// starting at 1 for the delay before the second operation attempt.
    /// </summary>
    public Func<int, TimeSpan>? DelayGenerator { get; init; }

    internal void Validate()
    {
        if (MaxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxAttempts),
                MaxAttempts,
                "MaxAttempts must be at least 1.");
        }
    }

    internal TimeSpan GetDelay(int retryNumber)
    {
        var delay = DelayGenerator?.Invoke(retryNumber) ?? TimeSpan.Zero;
        if (delay < TimeSpan.Zero)
            throw new InvalidOperationException("Retry delay cannot be negative.");
        return delay;
    }
}
