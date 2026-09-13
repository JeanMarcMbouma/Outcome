using System.Diagnostics.CodeAnalysis;

namespace BbQ.Outcome;

/// <summary>Returns true with a domain error when an exception is recognized; false preserves it.</summary>
/// <example><code>
/// static bool Map(Exception exception, out AppError error)
/// {
///     if (exception is TimeoutException)
///     {
///         error = AppError.DependencyTimeout;
///         return true;
///     }
///     error = default;
///     return false;
/// }
/// </code></example>
public delegate bool ExceptionMapper<TError>(Exception exception, [MaybeNullWhen(false)] out TError error);

/// <summary>An optional application service whose TryMap method can be supplied as a delegate.</summary>
public interface IExceptionMapper<TError>
{
    /// <summary>Attempts to translate a recognized exception.</summary>
    /// <example><code>
    /// var result = Outcome.Try(() => client.Load(), mapper.TryMap);
    /// </code></example>
    bool TryMap(Exception exception, [MaybeNullWhen(false)] out TError error);
}

/// <summary>Explicit boundaries for calling exception-based dependencies.</summary>
public static class Outcome
{
    /// <summary>
    /// Wraps a value-producing operation. Cancellation exceptions are never converted;
    /// unrecognized exceptions retain their original stack via rethrow.
    /// </summary>
    /// <example><code>
    /// var result = Outcome.Try(
    ///     () => legacyClient.Load(id),
    ///     exceptionMapper.TryMap);
    /// </code></example>
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
    /// <example><code>
    /// var result = await Outcome.TryAsync(
    ///     ct => legacyClient.LoadAsync(id, ct),
    ///     exceptionMapper.TryMap,
    ///     cancellationToken);
    /// </code></example>
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
    /// <example><code>
    /// Outcome&lt;User&gt; result = Outcome.Try(() => legacyClient.Load(id), MapException);
    /// </code></example>
    public static Outcome<T> Try<T>(Func<T> operation, ExceptionMapper<object?> mapper)
        => OutcomeInterop.Untyped(Try<T, object?>(operation, mapper));

    /// <summary>Explicit asynchronous exception conversion into a heterogeneous outcome.</summary>
    /// <example><code>
    /// Outcome&lt;User&gt; result = await Outcome.TryAsync(
    ///     ct => legacyClient.LoadAsync(id, ct), MapException, cancellationToken);
    /// </code></example>
    public static Task<Outcome<T>> TryAsync<T>(Func<CancellationToken, Task<T>> operation,
        ExceptionMapper<object?> mapper, CancellationToken cancellationToken = default)
        => OutcomeInterop.UntypedAsync(TryAsync<T, object?>(operation, mapper, cancellationToken));
}
