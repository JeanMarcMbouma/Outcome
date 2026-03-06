# BbQ.Outcome Benchmarks

BenchmarkDotNet suite for core `BbQ.Outcome` composition paths.

## Coverage

- `OutcomeCompositionBenchmarks`
  - `Map_*`, `Bind_*`, `Match_*`
  - typed error access (`HasErrors`, `GetError`)
  - `Combine` success/error paths
- `OutcomeAsyncBenchmarks`
  - `MapAsync_*`, `BindAsync_*`
  - `CombineAsync` success/error paths

## Run

From repository root:

```bash
dotnet run -c Release --project tests/Outcome.Benchmarks/Outcome.Benchmarks.csproj -- --filter * --join
```
