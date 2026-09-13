# BbQ Libraries — Outcome, CQRS & Events

A modern .NET library suite for functional result handling, CQRS, and event-driven systems. All libraries target .NET 8, .NET 9, and .NET 10.

## Packages

### Outcome family

Released together from `outcome-v*` tags with the same version.

| Package | Purpose |
|---|---|
| [BbQ.Outcome](./src/BbQ.Outcome/README.md) | Core functional result type, typed errors, async composition, LINQ, streaming and generated error helpers |
| `BbQ.Outcome.SourceGenerators` | Source generator for `[QbqOutcome]` error catalogs/helpers |
| [BbQ.Outcome.AspNetCore](./src/BbQ.Outcome.AspNetCore/README.md) | Explicit HTTP status mapping and redacted Problem Details |
| [BbQ.Outcome.SystemTextJson](./src/BbQ.Outcome.SystemTextJson/README.md) | Strict System.Text.Json contracts and Native AOT-friendly converters |
| [BbQ.Outcome.Diagnostics](./src/BbQ.Outcome.Diagnostics/README.md) | Opt-in structured logging and `Activity` instrumentation |

```bash
# Stable core
dotnet add package BbQ.Outcome

# Latest prerelease family packages
dotnet add package BbQ.Outcome --prerelease
dotnet add package BbQ.Outcome.AspNetCore --prerelease
dotnet add package BbQ.Outcome.SystemTextJson --prerelease
dotnet add package BbQ.Outcome.Diagnostics --prerelease
```

The core package remains independent of ASP.NET Core, JSON, logging, and telemetry dependencies. Install only the adapters your application needs.

### CQRS family

Released together from `cqrs-v*` tags with the same version.

| Package | Purpose |
|---|---|
| [BbQ.Cqrs](./src/BbQ.Cqrs/README.md) | Mediator, commands, queries, pipelines, streaming and dispatchers |
| `BbQ.Cqrs.SourceGenerators` | Compile-time handler and behavior registration |
| [BbQ.Cqrs.Outcome](./src/BbQ.Cqrs.Outcome/README.md) | Type-safe Outcome validation behaviors and response factories |
| [BbQ.Cqrs.Outcome.FluentValidation](./src/BbQ.Cqrs.Outcome.FluentValidation/README.md) | Async FluentValidation adapter for CQRS Outcome validation |

```bash
# Stable core
dotnet add package BbQ.Cqrs

# Latest prerelease family packages
dotnet add package BbQ.Cqrs --prerelease
dotnet add package BbQ.Cqrs.Outcome --prerelease
dotnet add package BbQ.Cqrs.Outcome.FluentValidation --prerelease
```

The CQRS core does not require Outcome or FluentValidation. The integration packages remain optional.

### Events family

| Package | Purpose |
|---|---|
| [BbQ.Events](./src/BbQ.Events/README.md) | Event publishing, handlers, subscribers, projections and replay |
| [BbQ.Events.SqlServer](./src/BbQ.Events.SqlServer/README.md) | SQL Server event store and checkpoint persistence |
| [BbQ.Events.PostgreSql](./src/BbQ.Events.PostgreSql/README.md) | PostgreSQL event store and checkpoint persistence |
| [BbQ.Events.RabbitMQ](./src/BbQ.Events.RabbitMQ/README.md) | RabbitMQ distributed event bus |

## Outcome highlights

`Outcome<T>` and `Outcome<T, TError>` support success/error composition without exceptions as ordinary control flow.

```csharp
var result = await repository.FindUser(userId)
    .MapError(MapPersistenceError)
    .Tap(user => audit.RecordUserRead(user.Id))
    .TapError(errors => diagnostics.RecordFailure(errors));
```

The newer composition surface includes error-side mapping and recovery, observation hooks, cancellation-aware async operators, `Sequence`, `Zip`, `Traverse`, bounded `TraverseAsync`, and explicit `Try`/`TryAsync` exception boundaries. Public extension APIs include inline XML examples so their intended scenarios are visible directly in IntelliSense.

See the [Outcome documentation](./src/BbQ.Outcome/README.md) and the [implementation/migration guide](./docs/extension-roadmap.md).

## CQRS + Outcome validation

Use `BbQ.Cqrs.Outcome` when requests return Outcome and validation failures should be created without unsafe casts or payload-type coupling.

```csharp
services.AddScoped<IRequestValidator<CreateUser>, CreateUserValidator>();
services.AddValidationIssueMapper<AppError>(issue =>
    new AppError(issue.Code, issue.Message, issue.MemberName));
services.AddOutcomeValidation<CreateUser, User, AppError>();
```

For FluentValidation:

```csharp
services.AddScoped<FluentValidation.IValidator<CreateUser>, CreateUserValidator>();
services.AddFluentValidationRequest<CreateUser>();
services.AddOutcomeValidation<CreateUser, User>();
```

## ASP.NET Core

`BbQ.Outcome.AspNetCore` maps domain errors to explicit HTTP policies while keeping transport concerns outside the core library.

```csharp
services.AddOutcomeHttpMapping<AppError>(options =>
{
    options.Rules["USER_MISSING"] = new(404, ExposeDescription: true);
    options.Rules["EMAIL_CONFLICT"] = new(409, ExposeDescription: true);
});

return outcome.ToIResult(httpMapper);
```

## JSON and Native AOT

`BbQ.Outcome.SystemTextJson` serializes only the active Outcome branch and rejects malformed/contradictory envelopes. It supports source-generated `JsonTypeInfo<T>` metadata and an explicit allowlist for heterogeneous error types.

## Diagnostics

`BbQ.Outcome.Diagnostics` adds opt-in logging and `Activity` observation without forcing a telemetry dependency into the core package.

## Release model

Package families are versioned and published together:

- `outcome-vX.Y.Z[-prerelease]` publishes the complete Outcome family.
- `cqrs-vX.Y.Z[-prerelease]` publishes the complete CQRS family.
- Events/provider packages retain their existing release tags.

Pre-release consumers should install with `--prerelease` or pin the exact preview version.

## Documentation

- [BbQ.Outcome](./src/BbQ.Outcome/README.md)
- [BbQ.Outcome.AspNetCore](./src/BbQ.Outcome.AspNetCore/README.md)
- [BbQ.Outcome.SystemTextJson](./src/BbQ.Outcome.SystemTextJson/README.md)
- [BbQ.Outcome.Diagnostics](./src/BbQ.Outcome.Diagnostics/README.md)
- [BbQ.Cqrs](./src/BbQ.Cqrs/README.md)
- [BbQ.Cqrs.Outcome](./src/BbQ.Cqrs.Outcome/README.md)
- [BbQ.Cqrs.Outcome.FluentValidation](./src/BbQ.Cqrs.Outcome.FluentValidation/README.md)
- [BbQ.Events](./src/BbQ.Events/README.md)
- [Outcome implementation and migration guide](./docs/extension-roadmap.md)

## License

MIT. See [LICENSE](./LICENSE).
