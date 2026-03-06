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
dotnet run -c Release --project tests/BbQ.Cqrs.Benchmarks/BbQ.Cqrs.Benchmarks.csproj -- --filter * --join
```

Run only dispatch benchmarks:

```bash
dotnet run -c Release --project tests/BbQ.Cqrs.Benchmarks/BbQ.Cqrs.Benchmarks.csproj -- --filter *CqrsDispatchBenchmarks* --join
```
