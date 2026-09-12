using System.Collections;

namespace BbQ.Outcome;

// Owns the array and exposes no mutable collection interface. The type is shared
// across value types so propagating a failure never copies an existing snapshot.
internal sealed class ErrorCollection<TError> : IReadOnlyList<TError>
{
    private readonly TError[] _items;

    private ErrorCollection(TError[] items) => _items = items;

    public static ErrorCollection<TError> Snapshot(IReadOnlyList<TError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors is ErrorCollection<TError> snapshot)
            return snapshot;
        if (errors.Count == 0)
            throw new ArgumentException("A failure must contain at least one error.", nameof(errors));

        var items = new TError[errors.Count];
        for (var i = 0; i < items.Length; i++)
        {
            var error = errors[i];
            if (error is null)
                throw new ArgumentException("A failure cannot contain a null error.", nameof(errors));
            items[i] = error;
        }
        return new ErrorCollection<TError>(items);
    }

    public static ErrorCollection<TError> Single(TError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ErrorCollection<TError>([error]);
    }

    public int Count => _items.Length;
    public TError this[int index] => _items[index];
    public IEnumerator<TError> GetEnumerator() => ((IEnumerable<TError>)_items).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
