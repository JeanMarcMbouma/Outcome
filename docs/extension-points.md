# Outcome extension points

Implementation of the core work in issue #60. Framework integrations remain optional; the core does not resolve services or depend on HTTP, validation, serialization, or telemetry frameworks.

## Failure invariants and migration

`FromErrors` now rejects a null list, an empty list, or a list containing null errors. `FromError` and implicit typed-error conversions reject a null error. Null **success values** remain supported.

This deliberately tightens previously accepted invalid inputs. Replace empty failures with an explicit success when there was no failure, or supply at least one meaningful error. Do not use `default(Outcome<T>)` or `default(Outcome<T, TError>)` to mean success or a valid failure: status checks remain unsuccessful, but consuming errors, deconstructing, matching, or aggregating a default instance throws `InvalidOperationException` instead of returning a null list or becoming a combined success. Some operations may simply propagate the invalid instance until it is consumed.

Failure factories snapshot externally supplied collections. Changing an input array/list after construction does not change an outcome. Error **objects** are not deep-cloned; use immutable error records when that guarantee is needed. Propagating an existing library-owned failure shares its immutable snapshot across successful-value types, avoiding repeated copies. A fresh external failure requires storage for its immutable snapshot; no zero-allocation claim is made for failure construction.

## Error translation, recovery and observation

```csharp
using BbQ.Outcome;

var failure = Outcome<int, string>.FromError("missing");
Outcome<int, int> translated = failure.MapError(error => error.Length);
var recovered = failure.Recover(_ => Outcome<int, string>.From(42));
var observed = recovered.Tap(Console.WriteLine)
    .TapError(errors => Console.Error.WriteLine(string.Join(", ", errors)));
```

`MapError` maps every error in input order. `MapErrors` calls its delegate once with the entire error list; the resulting list must remain a valid nonempty failure. Both preserve successful values and can change the error type. On `Outcome<T>`, these operators explicitly promote the heterogeneous error collection to `Outcome<T, TNewError>`.

`Recover` runs only on failure and may return success or another failure. `Tap` observes success; `TapError` observes the complete failure list. Observers return the original outcome and never swallow exceptions. Use an explicitly best-effort observer in application code when that behavior is required.

All new delegate arguments are validated eagerly, even on an inactive branch. Branch callbacks are invoked only on the relevant branch. Ordinary `Map` and `Bind` retain their existing exception semantics.

## Verification

`ExtensionPointTests` exercises invalid construction, default-state consumption, synchronous/asynchronous aggregation, structural immutability, failure snapshot reuse, error ordering, branch execution counts, recovery, heterogeneous-to-typed mapping, and exception propagation. Run the repository CI or:

```sh
dotnet test tests/BbQ.Outcome.Tests/BbQ.Outcome.Tests.csproj --configuration Release
```
