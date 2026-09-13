# Native AOT serialization smoke test

This executable exercises closed source-generated JSON metadata for both Outcome forms with reflection disabled. It checks active-branch serialization, typed and heterogeneous failures, exact registered error-type round trips, null successful values, and malformed-payload rejection.

```sh
dotnet publish samples/BbQ.Outcome.AotSmoke/BbQ.Outcome.AotSmoke.csproj -c Release -r linux-x64 -o artifacts/aot
./artifacts/aot/BbQ.Outcome.AotSmoke
```

The dedicated GitHub Actions workflow publishes and executes the native binary on Linux x64 with .NET 10. This is an actual publish/run check, not merely an `IsAotCompatible` declaration. Unit/integration tests separately cover .NET 8, 9, and 10; other AOT platforms are not implied by this smoke test.
