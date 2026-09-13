# Outcome extension roadmap implementation

Implementation of [issue #60](https://github.com/JeanMarcMbouma/Outcome/issues/60), separated into reviewable changes:

| PR | Scope | Dependency |
|---|---|---|
| [#61](https://github.com/JeanMarcMbouma/Outcome/pull/61) | Invariants, error-side/async/collection composition, exception boundaries, descriptors and catalogs | `master` |
| [#62](https://github.com/JeanMarcMbouma/Outcome/pull/62) | Validation factories, generated registrations, FluentValidation and HTTP adapters | #61 |
| [#63](https://github.com/JeanMarcMbouma/Outcome/pull/63) | JSON, diagnostics, generator diagnostic tests and end-to-end verification | #62 |

These are implementation branches, not a NuGet release. Merge in order and retarget each dependent PR to `master` once its predecessor is merged. Preserve commit ancestry with merge commits, or rebase/restack dependent branches after squash merging. Select release versions and update package dependency versions consistently before publication; no publication is performed by the verification workflows.

## Core additions

`MapError`, `MapErrors`, `Recover`, `Tap`, and `TapError` make the failure branch composable. Typed pipelines retain their error type unless explicitly mapped to another one. Heterogeneous error mapping can promote a pipeline to a typed error model. Delegates are validated eagerly, branch execution is explicit, and callback exceptions are not swallowed.

Token-aware asynchronous map/bind/match callbacks are awaited. Async stream callbacks are sequential. `Zip`, `Sequence`, and `Traverse` support aggregation; `TraverseAsync` schedules operation factories in bounded batches and preserves input ordering. Domain failures accumulate; unexpected faults cancel and drain started sibling tasks. `Try`/`TryAsync` are explicit exception boundaries, preserving cancellation and unmapped exceptions.

Error catalogs expose stable external codes, resource keys, and optional `IErrorDescriptorProvider<TError>` projections without constraining domain errors. The generator uses semantic attributes and fully qualified output identities, with `BBQOUT001` diagnostics for invalid/ambiguous definitions.

Detailed contracts: [core extensions and migration](extension-points.md), [async composition and catalogs](async-and-error-catalogs.md).

## Optional integration packages

| Package | Extension point | Documentation |
|---|---|---|
| BbQ.Cqrs.Outcome | `IValidationFailureFactory<TResponse>`, `IValidationIssueMapper<TError>`, two-parameter behavior | [README](../src/BbQ.Cqrs.Outcome/README.md) |
| BbQ.Cqrs.Outcome.FluentValidation | Async validator adapter preserving codes and member names | [README](../src/BbQ.Cqrs.Outcome.FluentValidation/README.md) |
| BbQ.Outcome.AspNetCore | `IOutcomeHttpMapper<TError>`, explicit status/exposure policy | [README](../src/BbQ.Outcome.AspNetCore/README.md) |
| BbQ.Outcome.SystemTextJson | Closed converters, strict envelopes, allowlisted error types | [README](../src/BbQ.Outcome.SystemTextJson/README.md) |
| BbQ.Outcome.Diagnostics | ILogger/Activity observation without sensitive payloads | [README](../src/BbQ.Outcome.Diagnostics/README.md) |

Framework dependencies flow into these packages, not back into the core libraries. The package verification script checks direct dependency sets and all .NET 8/9/10 assemblies.

## Compatibility and migration

- Replace `default(Outcome<...>)` with an explicit success or a failure containing meaningful errors. A default instance remains unsuccessful on status checks, but consuming it is invalid and throws.
- Failure factories now reject null lists, empty lists, and null errors. Empty aggregation input is still a successful empty result.
- Externally supplied failure collections are snapshotted. Error objects are not deep-cloned; use immutable records when needed. Existing internal snapshots are reused during propagation.
- Replace the sample three-parameter validation behavior with `ValidationBehavior<TRequest,TResponse>` and a response factory. The sample now returns validation failures rather than throwing a validation exception.
- Generated enum aliases are rejected because two named errors with the same underlying value cannot be distinguished by a runtime descriptor lookup. Use unique values. Duplicate external codes and blank resource keys are also rejected.
- JSON envelope names and registered error identifiers are explicit wire contracts. Unknown/duplicate fields, contradictory branches, and unregistered error types are rejected. This is not a permissive serializer for arbitrary objects.
- HTTP descriptions, targets, and metadata are redacted by default. Approve exposure per error code; severity never chooses status automatically.

These correctness changes may require a breaking-version release. Existing old package versions do not gain these APIs merely because the source branches exist.

## Verification

`CI` builds the solution and runs tests, including core and integration suites on .NET 8, 9, and 10. TRX reports and an aggregated summary distinguish passed, failed, and not-executed tests. Existing provider tests that require external services may remain skipped; those skips are not evidence of provider integration success.

`Outcome extension validation` performs a real .NET 10 Linux x64 Native AOT publish/run with JSON reflection disabled, packs and inspects all five optional packages without publishing, and runs ten BenchmarkDotNet success/failure cases. Artifacts are retained for 14 days. The native smoke test proves the tested configuration, not every AOT platform or arbitrary application-supplied converter.

The first full verification on head `8945c373c4530ed28964a77cd1a3889bd10e49dc` passed [solution CI](https://github.com/JeanMarcMbouma/Outcome/actions/runs/34724829441) and [AOT/package/benchmark checks](https://github.com/JeanMarcMbouma/Outcome/actions/runs/34724829505). Later commits add additional diagnostic tests/reporting; consult each PR's latest check results for its current head.

### Initial benchmark evidence

The first run used BenchmarkDotNet 0.15.6 ShortRun (one launch, three warmups, three measured iterations), .NET 10.0.12/SDK 10.0.401, on an AMD EPYC 7763 GitHub-hosted Linux runner. These are illustrative CI measurements, not a before/after regression claim or a production latency guarantee.

| Method | Success input | Mean | Allocated per operation |
|---|---:|---:|---:|
| Map | false | 1.788 ns | 0 B reported |
| MapError | false | 17.568 ns | 88 B |
| Recover | false | 1.793 ns | 0 B reported |
| Tap + TapError | false | 1.811 ns | 0 B reported |
| SnapshotExternalErrors | false | 12.632 ns | 56 B |
| Map | true | 1.188 ns | 0 B reported |
| MapError | true | 1.030 ns | 0 B reported |
| Recover | true | 1.021 ns | 0 B reported |
| Tap + TapError | true | 1.499 ns | 0 B reported |
| SnapshotExternalErrors | true | 12.524 ns | 56 B |

The snapshot benchmark always constructs a fresh failure; its success parameter does not change that operation. New mapped failures and external snapshots allocate, while the tested failure propagation and observer paths reused existing storage. The short run has limited statistical precision; retain its full report/error intervals and run longer benchmarks on representative hardware before setting thresholds. No `ValueTask` overload family was added without evidence justifying its API cost.

## Explicit follow-ups

- [#64](https://github.com/JeanMarcMbouma/Outcome/issues/64): Events serialization, stable identities, and upcasting.
- [#65](https://github.com/JeanMarcMbouma/Outcome/issues/65): Projection failure policy and durable dead-letter/checkpoint semantics.
- [#66](https://github.com/JeanMarcMbouma/Outcome/issues/66): Existing Source Link build dependency NU1902 warning. It remains visible and is not suppressed by this implementation.

The Events items were explicitly separated from the Outcome core roadmap in #60 and are not implemented by these three PRs.
