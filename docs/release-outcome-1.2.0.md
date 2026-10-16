# Outcome 1.2.0 — General availability

All five Outcome packages graduate from `1.2.0-preview.4` to `1.2.0`:
BbQ.Outcome, BbQ.Outcome.SourceGenerators, BbQ.Outcome.AspNetCore,
BbQ.Outcome.SystemTextJson and BbQ.Outcome.Diagnostics.

## Included capabilities

- Typed and heterogeneous outcomes with async composition, LINQ, collection operators, observation hooks and explicit exception boundaries.
- Explicit, dependency-free `Outcome.RetryAsync` with attempt limits, caller-defined backoff and cancellation. Exhaustion returns the final failure; unexpected exceptions propagate.
- Generated error catalogs and helpers.
- Optional ASP.NET Core status mapping and redacted Problem Details.
- Strict System.Text.Json envelopes with source-generated metadata and Native AOT-compatible converters.
- Optional structured logging and Activity instrumentation.
- Reduced error/collection allocations and centrally managed NuGet dependency versions.

## Upgrade

Pin each installed Outcome family package to `1.2.0`. No API or runtime behavior changes are introduced relative to preview.4. Core and integration packages retain .NET 8, 9 and 10 targets; the source generator retains .NET Standard 2.0 and Roslyn 4.8.

```sh
dotnet add package BbQ.Outcome --version 1.2.0
dotnet add package BbQ.Outcome.AspNetCore --version 1.2.0
dotnet add package BbQ.Outcome.SystemTextJson --version 1.2.0
dotnet add package BbQ.Outcome.Diagnostics --version 1.2.0
```

Install only the integrations needed. See the [migration guide](extension-roadmap.md),
[retry guidance](async-and-error-catalogs.md), and historical [preview.3](release-1.2.0-preview.3.md)
and [preview.4](release-1.2.0-preview.4.md) notes.

## Publication

After release PR checks pass and merge, publish `outcome-v1.2.0` from the validated master commit. The family workflow checks all five project versions, builds, runs core/generator/integration tests, packs and publishes through NuGet OIDC. Publish Outcome before CQRS `1.3.0`, whose integrations depend on Outcome `1.2.0`.
