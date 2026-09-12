using System.Diagnostics.CodeAnalysis;

namespace BbQ.Outcome;

/// <summary>Returns true with a domain error when an exception is recognized; false preserves it.</summary>
public delegate bool ExceptionMapper<TError>(Exception exception, [MaybeNullWhen(false)] out TError error);

/// <summary>An optional application service whose TryMap method can be supplied as a delegate.</summary>
public interface IExceptionMapper<TError>
{
    /// <summary>Attempts to translate a recognized exception.</summary>
    bool TryMap(Exception exception, [MaybeNullWhen(false)] out TError error);
}

/// <summary>Explicit boundaries for calling exception-based dependencies.</summary>
public static class Outcome
{
    /// <summary>
    /// Wraps a value-producing operation. Cancellation exceptions are never converted;
    /// unrecognized exceptions retain their original stack via rethrow.
    /// </summary>
    public static Outcome<T, TError> Try<T, TError>(Func<T> operation, ExceptionMapper<TError> mapper)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(mapper);
        try { return Outcome<T, TError>.From(operation()); }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            if (!mapper(exception, out var error))
                throw;
            return Outcome<T, TError>.FromError(error);
        }
    }

    /// <summary>Calls an asynchronous dependency with cancellation and explicit exception mapping.</summary>
    public static async Task<Outcome<T, TError>> TryAsync<T, TError>(
        Func<CancellationToken, Task<T>> operation, ExceptionMapper<TError> mapper,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(mapper);
        cancellationToken.ThrowIfCancellationRequested();
        try { return Outcome<T, TError>.From(await operation(cancellationToken).ConfigureAwait(false)); }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            if (!mapper(exception, out var error))
                throw;
            return Outcome<T, TError>.FromError(error);
        }
    }

    /// <summary>Explicit exception conversion into a heterogeneous outcome.</summary>
    public static Outcome<T> Try<T>(Func<T> operation, ExceptionMapper<object?> mapper)
        => OutcomeInterop.Untyped(Try<T, object?>(operation, mapper));

    /// <summary>Explicit asynchronous exception conversion into a heterogeneous outcome.</summary>
    public static Task<Outcome<T>> TryAsync<T>(Func<CancellationToken, Task<T>> operation,
        ExceptionMapper<object?> mapper, CancellationToken cancellationToken = default)
        => OutcomeInterop.UntypedAsync(TryAsync<T, object?>(operation, mapper, cancellationToken));
}
