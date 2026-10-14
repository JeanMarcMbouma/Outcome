# CQRS 1.3.0-preview.4

This preview simplifies mediator and Outcome validation setup (#81).

- `AddBbQMediatorWithOutcomeValidationFromAssemblies(assemblies, configureValidation)` registers mediator services, handlers, validators and Outcome validation from explicitly supplied assemblies.
- Generated `Add{Assembly}MediatorWithOutcomeValidation(configureValidation)` provides equivalent setup with direct registrations and no runtime assembly scanning.
- Optional `AddBbQMediatorWithOutcomeAndFluentValidationFromAssemblies` and generated `Add{Assembly}MediatorWithOutcomeAndFluentValidation` discover scoped FluentValidation validators and add asynchronous per-request adapters. Native validators coexist; multiple FluentValidation validators run sequentially.
- Shared `MapIssuesTo<TError>` configuration maps validation issues to domain errors. `MapIssuesTo<TError,TMapper>` supports scoped mapper dependencies.
- Registration preserves custom factories and mediator infrastructure and avoids duplicate identical handlers, validators, factories and validation behaviors.
- Generated setup includes direct requests, commands, queries, streaming and fire-and-forget handlers. Existing explicit registration APIs remain available.
- Domain error mappings and custom pipeline behaviors remain explicit. Generated registration does not remove existing runtime reflection from mediator dispatch.

All four CQRS packages share version `1.3.0-preview.4`:

| Package | Targets |
|---|---|
| BbQ.Cqrs | .NET 8, 9, 10 and .NET Framework 4.8.1 |
| BbQ.Cqrs.SourceGenerators | .NET Standard 2.0 (Roslyn 4.8 baseline) |
| BbQ.Cqrs.Outcome | .NET 8, 9 and 10 |
| BbQ.Cqrs.Outcome.FluentValidation | .NET 8, 9 and 10 |

Internal CQRS package dependencies use the matching preview. Outcome remains independently versioned at `1.2.0-preview.4`; optional Outcome/FluentValidation integrations remain optional.

Install the packages needed by the application, pinning the same CQRS preview:

```sh
dotnet add package BbQ.Cqrs --version 1.3.0-preview.4
dotnet add package BbQ.Cqrs.SourceGenerators --version 1.3.0-preview.4
dotnet add package BbQ.Cqrs.Outcome --version 1.3.0-preview.4
dotnet add package BbQ.Cqrs.Outcome.FluentValidation --version 1.3.0-preview.4
```

See the [registration guide](../src/BbQ.Cqrs.Outcome/README.md) for scanning, generated setup, domain mappings and startup dependency validation.

## Publishing after merge

The release tag is `cqrs-v1.3.0-preview.4`. The existing CQRS publishing workflow checks every family project's version against the tag, runs CQRS, source-generator, registration and integration tests, packs the four packages and publishes them through NuGet OIDC. This PR prepares the release; creating the tag initiates publication.
