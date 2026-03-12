# BbQ.Events Benchmarks

This project contains BenchmarkDotNet performance benchmarks for core `BbQ.Events` paths.

## Coverage

- `EventStoreBenchmarks`
  - `AppendSingleEvent`
  - `ReadAllFromStart`
- `EventBusBenchmarks`
  - `PublishWithoutHandlers`
  - `PublishWithSingleHandler`
  - `PublishWithActiveSubscriber`
  - `PublishWithTwoActiveSubscribers`

## Run

From repository root:

```bash
dotnet run -c Release --framework net10.0 --project tests/BbQ.Events.Benchmarks/BbQ.Events.Benchmarks.csproj -- --filter * --join
```

Run only EventStore benchmarks:

```bash
dotnet run -c Release --framework net10.0 --project tests/BbQ.Events.Benchmarks/BbQ.Events.Benchmarks.csproj -- --filter *EventStoreBenchmarks* --join
```

Run only EventBus benchmarks:

```bash
dotnet run -c Release --framework net10.0 --project tests/BbQ.Events.Benchmarks/BbQ.Events.Benchmarks.csproj -- --filter *EventBusBenchmarks* --join
```

## Latest findings by runtime (2026-03-12)

Source report: `BenchmarkDotNet.Artifacts/results/BenchmarkRun-joined-2026-03-12-15-56-40-report.csv`

| Runtime | Benchmarks | Avg Mean (ns) | Min (ns) | Max (ns) |
|---|---:|---:|---:|---:|
| .NET 8.0 | 8 | 463.52 | 312.30 | 691.40 |
| .NET 9.0 | 8 | 473.92 | 356.90 | 712.80 |
| .NET 10.0 | 8 | 342.28 | 210.30 | 509.30 |

- Fastest average runtime: **.NET 10.0**
