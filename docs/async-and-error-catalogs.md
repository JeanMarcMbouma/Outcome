# Async composition, collections, and error catalogs

See [extension-points.md](extension-points.md) for the failure-state migration and error-side operators.

## Token-aware callbacks

The new overloads accept `Func<T, CancellationToken, Task<...>>`. Unlike the original task-based `MatchAsync` with synchronous callbacks, the new branch callbacks are actually awaited.

```csharp
var result = await LoadOutcomeAsync()
    .BindAsync((value, ct) => SaveOutcomeAsync(value, ct), cancellationToken);

var response = await result.MatchAsync(
    (value, ct) => FormatSuccessAsync(value, ct),
    (errors, ct) => FormatFailureAsync(errors, ct),
    cancellationToken);
```

`MapAsync`, `BindAsync`, `MatchAsync`, `TapAsync`, `TapErrorAsync`, and `RecoverAsync` are available on typed and heterogeneous outcomes. Task-source overloads exist for mapping, binding, and matching. Cancelling the wait for an already-running task does **not** cancel its producer; the producing operation must receive a token separately.

Stream `MapAsync` and `BindAsync` await callbacks sequentially, preserve failures, and combine explicitly supplied and enumeration cancellation through the async-iterator contract. No implicit retry or exception conversion occurs.

## Collection composition

`Zip` combines different success types into a named tuple, accumulating errors left then right. `Sequence` enumerates once and returns `Outcome<IReadOnlyList<T>, TError>` (or the heterogeneous counterpart). Empty input is a successful empty collection. `Traverse` applies an outcome-producing function to every input before accumulating failures.

```csharp
var result = await inputs.TraverseAsync(
    (input, ct) => ProcessAsync(input, ct),
    maxConcurrency: 4,
    cancellationToken: cancellationToken);
```

`TraverseAsync` accepts operation factories, not pre-started tasks. It schedules bounded **batches**, retaining input order regardless of completion order. Domain failures use accumulate-all behavior; there is no fail-fast domain mode in this API. Unexpected exceptions cancel sibling work, preserve the original fault where cancellation was internal, and observe every started task before returning. Callbacks must cooperate with cancellation. Only the current batch's tasks are retained, but results are retained until aggregation, so result memory is proportional to input size. Source enumeration is single-threaded and the enumerator is disposed normally.

## Explicit exception boundaries

```csharp
static bool MapException(Exception exception, out string error)
{
    error = "STORAGE_UNAVAILABLE";
    return exception is IOException;
}

var result = await Outcome.TryAsync(
    ct => ReadFromDependencyAsync(ct),
    MapException,
    cancellationToken);
```

`Try`/`TryAsync` invoke the mapper only for non-cancellation exceptions. Returning false rethrows the original exception. Returning true requires a non-null error. Mapper exceptions also remain visible. Ordinary `Map` and `Bind` are unchanged. An `IExceptionMapper<TError>` service may provide its `TryMap` method as the delegate.

## Error descriptors and generated catalogs

`IErrorDescriptorProvider<TError>` is optional: `TError` remains unconstrained. Its `ErrorDescriptor` provides an external code, message, severity, optional target, approved metadata, and optional localization resource key. No localization or transport service is resolved by the core.

```csharp
[QbqOutcome]
public enum UserError
{
    [ErrorCode("USER_MISSING")]
    [ErrorResourceKey("Errors.UserMissing")]
    [System.ComponentModel.Description("User was not found.")]
    Missing
}

var descriptor = UserErrorErrors.Describe(UserError.Missing);
var allErrors = UserErrorErrors.All;
var provider = UserErrorErrors.DescriptorProvider;
```

Existing `{Member}Error` properties remain available. `Describe` returns the catalog definition; `DescriptorProvider` also preserves the individual error occurrence's description and severity. External codes default to member names, but explicit codes are recommended for contracts that must survive member renames. Resource keys are not resolved until an application adapter chooses a culture.

The generator uses semantic attributes, fully qualified type references, properly escaped string literals, and identity-based output filenames. Equal enum names in different namespaces no longer collide. Duplicate external codes, enum-value aliases, blank codes/resource keys, inaccessible nested types, and generic containing types produce `BBQOUT001` diagnostics. Nested nongeneric enum helpers include containing type names.

## Performance scope

`ExtensionPointsBenchmarks` measures both success and failure branches plus external error snapshot creation. It is included in the existing benchmark switcher. No new `ValueTask` overload family is introduced: that API expansion remains deferred until representative measurements justify it. Fresh failure snapshots allocate; propagation of an existing snapshot reuses it.
