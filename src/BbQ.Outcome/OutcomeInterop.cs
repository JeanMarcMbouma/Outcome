namespace BbQ.Outcome;

internal static class OutcomeInterop
{
    internal static Outcome<T, object?> Typed<T>(Outcome<T> outcome)
        => outcome.IsSuccess ? Outcome<T, object?>.From(outcome.ValueUnchecked)
            : Outcome<T, object?>.FromErrors(outcome.ErrorsUnchecked);

    internal static Outcome<T> Untyped<T>(Outcome<T, object?> outcome)
        => outcome.IsSuccess ? Outcome<T>.From(outcome.ValueUnchecked)
            : Outcome<T>.FromErrors(outcome.ErrorsUnchecked);

    internal static async Task<Outcome<T>> UntypedAsync<T>(Task<Outcome<T, object?>> task)
        => Untyped(await task.ConfigureAwait(false));
}
