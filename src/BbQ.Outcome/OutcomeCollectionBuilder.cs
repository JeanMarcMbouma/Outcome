namespace BbQ.Outcome;

// Per-operation state. A single failure reuses its immutable snapshot; success
// values are discarded after the first failure, but all inputs are still visited.
internal struct OutcomeCollectionBuilder<T, TError>(int capacity)
{
    private List<T>? _values;
    private IReadOnlyList<TError>? _firstErrors;
    private List<TError>? _combinedErrors;

    internal void Add(Outcome<T, TError> outcome)
    {
        if (outcome.IsSuccess)
        {
            if (_firstErrors is null)
                (_values ??= new List<T>(capacity)).Add(outcome.ValueUnchecked);
            return;
        }

        var errors = outcome.ErrorsUnchecked;
        _values = null;
        if (_firstErrors is null)
        {
            _firstErrors = errors;
            return;
        }

        if (_combinedErrors is null)
        {
            _combinedErrors = new List<TError>(Math.Max(4, _firstErrors.Count + errors.Count));
            AppendErrors(_firstErrors);
        }
        AppendErrors(errors);
    }

    private void AppendErrors(IReadOnlyList<TError> errors)
    {
        // Indexing avoids allocating an enumerator for each error collection.
        for (var i = 0; i < errors.Count; i++)
            _combinedErrors!.Add(errors[i]);
    }

    private IReadOnlyList<TError> Errors => _combinedErrors is null
        ? _firstErrors!
        : ErrorCollection<TError>.FromOwnedArray(_combinedErrors.ToArray());

    internal Outcome<IReadOnlyList<T>, TError> BuildReadOnly() => _firstErrors is not null
        ? Outcome<IReadOnlyList<T>, TError>.FromErrors(Errors)
        : Outcome<IReadOnlyList<T>, TError>.From(_values is null ? Array.Empty<T>() : _values.AsReadOnly());

    internal Outcome<IEnumerable<T>, TError> BuildEnumerable() => _firstErrors is not null
        ? Outcome<IEnumerable<T>, TError>.FromErrors(Errors)
        : Outcome<IEnumerable<T>, TError>.From(_values ?? (IEnumerable<T>)Array.Empty<T>());
}
