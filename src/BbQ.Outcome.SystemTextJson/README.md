# BbQ.Outcome.SystemTextJson

Optional, explicit System.Text.Json converters for .NET 8, 9, and 10. No JSON dependency is introduced into the core Outcome project. Closed converters consume `JsonTypeInfo<T>` rather than creating types or searching assemblies at runtime.

## Wire format

Typed success: `{"isSuccess":true,"value":42}`

Typed failure: `{"isSuccess":false,"errors":[{"code":"MISSING","message":"Not found"}]}`

Only the active branch is serialized. A null successful value is allowed when the value type supports it. Null outcomes, default/uninitialized outcomes, missing or nonboolean flags, duplicate or unknown envelope fields, contradictory branches, empty errors, and null error entries are rejected. Reading malformed envelopes throws `JsonException`; serializing an uninitialized outcome throws the core `InvalidOperationException`.

The envelope is deliberately strict and case-sensitive. Customize its three names with `OutcomeJsonEnvelope`; they must be nonblank and distinct. Payload naming, enum representation, and custom payload conversion are determined by the **supplied payload metadata**, not implicitly inherited from the outer serializer options. Changes to envelope names or error type identifiers require an application-level wire-contract migration.

## Registration and Native AOT

```csharp
var payload = WireContext.Default;
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
options.AddOutcomeConverter(payload.User, payload.AppError);
var wire = new WireContext(options);

string json = JsonSerializer.Serialize(outcome, wire.UserOutcome);
var restored = JsonSerializer.Deserialize(json, wire.UserOutcome);

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(User))]
[JsonSerializable(typeof(AppError))]
[JsonSerializable(typeof(Outcome<User, AppError>), TypeInfoPropertyName = "UserOutcome")]
internal partial class WireContext : JsonSerializerContext { }
```

Include each closed Outcome root in the source-generated context, register its converter on fresh options, and create the context with those options. Supply the root metadata to serialize/deserialize calls. There is no reflection-based open-generic factory. The `samples/BbQ.Outcome.AotSmoke` project verifies both Outcome forms with reflection disabled and a real Native AOT publish/run.

## Heterogeneous error allowlist

```csharp
var registry = new OutcomeErrorTypeRegistryBuilder()
    .Register("app-error-v1", payload.AppError)
    .Register("text-v1", payload.String)
    .Build();
options.AddOutcomeConverter(payload.User, registry);
```

A heterogeneous failure uses an explicit tagged error envelope:

```json
{"isSuccess":false,"errors":[
  {"type":"app-error-v1","error":{"code":"MISSING","message":"Not found"}},
  {"type":"text-v1","error":"Another error"}
]}
```

Unknown identifiers, missing/null payloads, and duplicate or extra tag-envelope fields are rejected. Writing an unregistered exact runtime type throws `NotSupportedException`. Registering duplicate types or identifiers throws. A registry is an immutable snapshot of its builder; callers cannot mutate an active registry. It never calls `Type.GetType`, loads an assembly, or trusts a CLR type name from JSON. Explicitly supplied payload converters remain application-owned and must themselves be safe for untrusted input and compatible with trimming/AOT when required.

## Scope

These converters define a storage/message wire contract, not a public API exposure policy. Use the separate ASP.NET Core adapter for status mapping and redacted Problem Details. The converters buffer one outcome with `JsonDocument`; enforce request/message size limits at the application boundary. Generic payload object graphs follow System.Text.Json depth limits and the provided metadata's validation semantics.
