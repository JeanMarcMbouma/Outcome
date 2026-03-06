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
dotnet run -c Release --project tests/BbQ.Events.Benchmarks/BbQ.Events.Benchmarks.csproj -- --filter * --join
```

Run only EventStore benchmarks:

```bash
dotnet run -c Release --project tests/BbQ.Events.Benchmarks/BbQ.Events.Benchmarks.csproj -- --filter *EventStoreBenchmarks* --join
```

Run only EventBus benchmarks:

```bash
dotnet run -c Release --project tests/BbQ.Events.Benchmarks/BbQ.Events.Benchmarks.csproj -- --filter *EventBusBenchmarks* --join
```
