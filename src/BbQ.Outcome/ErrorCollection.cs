using System.Collections;
using System.Runtime.CompilerServices;

namespace BbQ.Outcome;

// Owns its storage and exposes no mutable collection interface. The type is shared
// across value types so propagating a failure never copies an existing snapshot.
internal abstract class ErrorCollection<TError> : IReadOnlyList<TError>
{
    private ErrorCollection() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ErrorCollection<TError> Snapshot(IReadOnlyList<TError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors is ErrorCollection<TError> snapshot)
            return snapshot;
        if (errors is TError[] array && array.Length > 1)
            return CopyArray(array);
        return CopyExternal(errors);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ErrorCollection<TError> CopyArray(TError[] errors)
    {
        // Keep this common path small enough to specialize for array callers.
        var copy = new TError[errors.Length];
        for (var i = 0; i < errors.Length; i++)
        {
            var error = errors[i];
            if (error is null)
                throw new ArgumentException("A failure cannot contain a null error.", nameof(errors));
            copy[i] = error;
        }
        return new MultipleErrors(copy);
    }

    private static ErrorCollection<TError> CopyExternal(IReadOnlyList<TError> errors)
    {
        var count = errors.Count;
        if (count == 0)
            throw new ArgumentException("A failure must contain at least one error.", nameof(errors));
        if (count == 1)
        {
            var error = errors[0];
            if (error is null)
                throw new ArgumentException("A failure cannot contain a null error.", nameof(errors));
            return new SingleError(error);
        }

        var items = new TError[count];
        for (var i = 0; i < items.Length; i++)
        {
            var error = errors[i];
            if (error is null)
                throw new ArgumentException("A failure cannot contain a null error.", nameof(errors));
            items[i] = error;
        }
        return new MultipleErrors(items);
    }

    public static ErrorCollection<TError> Single(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new SingleError(error);
    }

    // Only pass a fresh, nonempty array of validated errors, and relinquish ownership.
    internal static ErrorCollection<TError> FromOwnedArray(TError[] items) => new MultipleErrors(items);

    public abstract int Count { get; }
    public abstract TError this[int index] { get; }
    public abstract IEnumerator<TError> GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private sealed class SingleError(TError error) : ErrorCollection<TError>
    {
        public override int Count => 1;
        public override TError this[int index] => index == 0 ? error : throw new IndexOutOfRangeException();
        public override IEnumerator<TError> GetEnumerator()
        {
            yield return error;
        }
    }

    private sealed class MultipleErrors(TError[] items) : ErrorCollection<TError>
    {
        public override int Count => items.Length;
        public override TError this[int index] => items[index];
        public override IEnumerator<TError> GetEnumerator() => ((IEnumerable<TError>)items).GetEnumerator();
    }
}
