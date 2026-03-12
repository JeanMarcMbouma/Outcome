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
dotnet run -c Release --framework net10.0 --project tests/BbQ.Outcome.Benchmarks/BbQ.Outcome.Benchmarks.csproj -- --filter * --join
```

## Latest findings by runtime (2026-03-12)

Source report: `BenchmarkDotNet.Artifacts/results/BenchmarkRun-joined-2026-03-12-16-12-47-report.csv`

| Runtime | Benchmarks | Avg Mean (ns) | Min (ns) | Max (ns) |
|---|---:|---:|---:|---:|
| .NET 8.0 | 16 | 57.29 | 1.44 | 311.79 |
| .NET 9.0 | 16 | 57.13 | 2.00 | 330.97 |
| .NET 10.0 | 16 | 44.11 | 2.28 | 210.75 |

- Fastest average runtime: **.NET 10.0**
