# BbQ.Cqrs Benchmarks

This project contains BenchmarkDotNet performance benchmarks for core `BbQ.Cqrs` dispatch paths.

## Coverage

- `CqrsDispatchBenchmarks`
  - `CommandDispatch_NoBehavior`
  - `CommandDispatch_OneBehavior`
  - `QueryDispatch_NoBehavior`
  - `QueryDispatch_OneBehavior`
  - `MediatorSend_Command_NoBehavior`
  - `MediatorSend_Query_OneBehavior`

- `CqrsStreamBenchmarks`
  - `Stream_NoBehavior`
  - `Stream_OneBehavior`

## Run

From repository root:

```bash
dotnet run -c Release --framework net10.0 --project tests/BbQ.Cqrs.Benchmarks/BbQ.Cqrs.Benchmarks.csproj -- --filter * --join
```

Run only dispatch benchmarks:

```bash
dotnet run -c Release --framework net10.0 --project tests/BbQ.Cqrs.Benchmarks/BbQ.Cqrs.Benchmarks.csproj -- --filter *CqrsDispatchBenchmarks* --join
```

## Latest findings by runtime (2026-03-12)

Source report: `BenchmarkDotNet.Artifacts/results/BenchmarkRun-joined-2026-03-12-15-47-36-report.csv`

| Runtime | Benchmarks | Avg Mean (ns) | Min (ns) | Max (ns) |
|---|---:|---:|---:|---:|
| .NET 8.0 | 10 | 136.02 | 100.93 | 179.45 |
| .NET 9.0 | 10 | 158.91 | 131.58 | 184.37 |
| .NET 10.0 | 10 | 117.78 | 73.26 | 186.37 |

- Fastest average runtime: **.NET 10.0**
