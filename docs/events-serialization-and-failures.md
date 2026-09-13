# Event serialization and projection failure operations

## Shared serialization (#64)

SQL Server, PostgreSQL and RabbitMQ all accept `options.EventSerializer` (`IEventSerializer`). A null setting uses `LegacyJsonEventSerializer`: existing camel-case JSON, configured `JsonSerializerOptions`, and the expected generic event type's full name. It never loads a type named by input. Existing callers and stored bare JSON remain compatible.

To opt into stable identities, configure the same closed registry and serializer on every provider:

```csharp
var registry = new EventTypeRegistry()
    .Register<OrderChanged>("orders.changed", version: 2, "Old.Application.OrderChanged");
var serializer = new JsonEventSerializer(registry, new[] { new OrderV1ToV2() });
// Assign serializer to each database/broker options.EventSerializer.
```

Canonical identities use envelope format 1: `{"$eventEnvelope":1,"schemaVersion":2,"payload":{...}}`. The database EventType column and RabbitMQ Type/header/routing key carry `orders.changed`. Envelope versions describe the container; schema versions describe one event's payload. These are independent of the Outcome JSON contract.

`IEventUpcaster` transforms version N to N+1 using `JsonElement`; register exactly one step per identity/from-version. Missing steps, ambiguous steps, unknown identities, future versions, null/malformed payloads and mismatched requested event types fail closed. Known other event types are filtered by typed database reads. Upcasters must be deterministic, pure and free of network/time/random dependencies. They operate on detached JSON; persisted history is never rewritten.

Legacy migration is explicit: register old full names as aliases. Alias payloads are interpreted as bare schema version 1. Canonical identities always require envelopes, avoiding accidental envelope detection in old application data. Keep aliases while historical records remain. Deploy readers with aliases/upcasters before enabling canonical writers. Plan RabbitMQ queue bindings separately: canonical writers use the stable routing key; legacy queues remain bound to the old key and must be drained or explicitly rebound during rollout. Existing defaults do not change queue bindings.

`RegisterMetadata(JsonTypeInfo<T>)` accepts System.Text.Json source-generated payload metadata. Unregistered metadata falls back to configured JSON options/reflection. This does not claim that the reflection-based projection engine or database/broker adapters support Native AOT.

## Failure policies and checkpoints (#65)

Set `ProjectionErrorHandlingOptions.FailurePolicy` or register `IProjectionFailurePolicy` in DI. A per-projection policy takes precedence. Context includes projection, partition, events/IDs/types, exception, attempt number and local checkpoint ordinal. The default retains the existing total attempt count (`MaxRetryAttempts`, including the initial attempt), exponential delay, cap, and fallback. Custom policies cannot exceed that cap. Cancellation is propagated without classification; policy exceptions and persistence errors stop the worker and propagate when the engine joins it.

| Decision | Handler behavior | Checkpoint behavior | Monitoring |
| --- | --- | --- | --- |
| Retry | Invoke again after bounded backoff | No advancement until terminal disposition | No success counted for failed attempts |
| Stop | Stop partition | Failed item/batch not checkpointed | Stopped |
| Skip | Continue without persistence | Advance according to normal checkpoint batching | Skipped, never projected |
| Quarantine | Persist each failed event before continuing | Advance only after all writes succeed | Quarantined, never projected |

Batch failure applies to the whole handler invocation: batch handlers must be idempotent because retries can repeat partial effects. Adjacent handler/type groups retain partition order. Successful batch events are counted individually. `IProjectionDispositionMonitor` is an optional extension for custom monitors; the built-in monitor has separate skipped/quarantined/stopped counters.

Quarantine requires an `IProjectionDeadLetterStore`, `EventIdSelector` returning a stable application event ID, and `SerializeDeadLetterEvent` using your shared serializer. Do not use a local counter as an event ID. For example:

```csharp
options.ErrorHandling.Strategy = ProjectionErrorHandlingStrategy.Retry;
options.ErrorHandling.FallbackStrategy = ProjectionErrorHandlingStrategy.Quarantine;
options.ErrorHandling.DeadLetterStore = sqlDeadLetters;
options.ErrorHandling.EventIdSelector = value => ((OrderChanged)value).Id;
options.ErrorHandling.SerializeDeadLetterEvent = value => serializer.Serialize((OrderChanged)value);
```

`SqlServerProjectionDeadLetterStore.InitializeAsync` creates the append-only quarantine table. Each Put commits an independent SQL transaction, deduplicates by the hash of projection/partition/application event ID, and rejects an ID collision with different event data. Payloads retain replay data; exception messages, stack traces and arbitrary exception data are excluded (only exception type is stored). Apply database access control, encryption and retention appropriate to event data. The implementation does not redact the event payload itself.

There is **no cross-store atomic transaction** with the checkpoint. A crash after persistence but before checkpointing can redeliver the event. When all entries already exist, the processor validates payload equality and returns quarantined without invoking the handler. Partial batch persistence retries the whole batch; writes are idempotent. If the checkpoint write fails, retained records allow the same recovery on redelivery. Keep records through replay; deleting them removes this recovery protection.

The existing live engine uses local per-partition processing ordinals and `IEventBus` does not expose broker acknowledgements or durable source offsets. These checkpoint guarantees apply to engine progress, not end-to-end RabbitMQ delivery: that adapter acknowledges after enqueueing to its subscription bridge. To recover after a broker/process loss, retain events in a durable event store and use the application's replay flow. This change does not claim exactly-once delivery or replace the replay engine.

## Inspect and replay quarantine

The runnable `samples/BbQ.Events.Quarantine` sample uses the `orders.changed` model. Adapt its registry to your application's events. Set `QUARANTINE_SQL_CONNECTION`, then run:

```text
dotnet run --project samples/BbQ.Events.Quarantine -- list
dotnet run --project samples/BbQ.Events.Quarantine -- replay <dead-letter-id> replay-investigation
```

Inspection prints identities and sanitized error codes, not payloads. Replay appends the selected event to an isolated stream and retains the original dead letter. After fixing the handler/schema, process that stream with an idempotent handler in a controlled recovery flow. Repeated replay commands append repeated deliveries: use event IDs for deduplication. Do not reset a production checkpoint blindly or assume replay deletes/acknowledges quarantine records.

## Source Link maintenance (#66)

The four direct `Microsoft.SourceLink.GitHub` 8.0.0 references (Outcome and the three generators) pulled in `Microsoft.Build.Tasks.Git` 8.0.0. They are removed in favor of SDK-provided Source Link. `global.json` requires the patched SDK 10.0.303 or a later 10.0 feature band, with preview SDKs disabled. Repository URLs, embedded untracked sources, portable symbols and snupkg settings are retained. Targeting net481 still uses the modern SDK; it does not require an old Source Link task.

Advisory: https://github.com/advisories/GHSA-23fw-v26w-5fgq

NuGet audit remains enabled. Run the full solution build/tests, package creation and existing Outcome AOT publish/run smoke. `Events provider validation` starts real SQL Server, PostgreSQL and RabbitMQ services and fails when a required service is missing. Local skipped provider tests are not evidence of durability.

The real-service checks also correct PostgreSQL's nullable checkpoint uniqueness to a PostgreSQL 15+ "UNIQUE NULLS NOT DISTINCT" constraint. Existing SQL Server checkpoint tests use a nullable unique constraint instead of an invalid nullable primary key. Provider fixture setup failures are fatal when REQUIRE_EVENT_SERVICES=1. PostgreSQL tests use the provisioned service directly, removing their old Testcontainers/SSH.NET dependency.
