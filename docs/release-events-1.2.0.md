# Events 1.2.0 — General availability

All five Events packages share stable version `1.2.0` and now publish together:

| Package | Previous project version | GA version |
|---|---|---|
| BbQ.Events | 1.2.0-preview.1 | 1.2.0 |
| BbQ.Events.SourceGenerators | 1.2.0-preview.1 | 1.2.0 |
| BbQ.Events.SqlServer | 0.3.0-preview.1 | 1.2.0 |
| BbQ.Events.PostgreSql | 1.1.0-preview.1 | 1.2.0 |
| BbQ.Events.RabbitMQ | 1.2.0-preview.1 | 1.2.0 |

This promotes the existing implementations without changing public APIs, runtime behavior or database schemas. Core and providers retain .NET 8, 9 and 10; the source generator retains .NET Standard 2.0 and its Roslyn 4.8 baseline. Provider versions deliberately converge on the family version, including SQL Server's graduation from its 0.x preview.

## Included capabilities

- In-memory event publishing, handlers and asynchronous subscriptions.
- Projections, partitioned/batch processing, checkpoints and replay.
- Generated event handler, subscriber and projection registration.
- Explicit serialization, event type registration and upcasting.
- Optional SQL Server and PostgreSQL durable stores and checkpoint persistence.
- Optional RabbitMQ distributed event bus.
- Previously merged SQL Server first-checkpoint-write concurrency fix.

## Upgrade

Update every installed Events package to `1.2.0`; providers depend on stable core `1.2.0`, not a preview. Install only the providers needed:

```sh
dotnet add package BbQ.Events --version 1.2.0
dotnet add package BbQ.Events.SourceGenerators --version 1.2.0
dotnet add package BbQ.Events.SqlServer --version 1.2.0
dotnet add package BbQ.Events.PostgreSql --version 1.2.0
dotnet add package BbQ.Events.RabbitMQ --version 1.2.0
```

Retain existing serializer/type registrations, schema deployment and broker configuration. GA does not implicitly change existing persisted data or broker durability settings. See [serialization and failure guidance](events-serialization-and-failures.md) and each provider README.

## Release validation and publication

After the release PR passes CI and real-provider validation, merge and publish `events-v1.2.0` from that validated master commit. The main publishing workflow delegates to the Events family workflow; there is only one tag-triggered family publisher.

Publication first runs the reusable provider-validation workflow with required SQL Server, PostgreSQL and RabbitMQ services on .NET 8, 9 and 10, the existing provider suites, CQRS/Events behavior tests, Native AOT smoke execution, full solution build/pack, internal dependency checks and Source Link verification. A failed validation blocks all NuGet pushes.

Every project's version must match the tag. The publisher packs exactly the five family packages, publishes the generator and core before providers, then publishes symbols using NuGet OIDC. Legacy provider-specific tag workflows remain available for individual maintenance releases. Outcome and CQRS versions are unchanged.
