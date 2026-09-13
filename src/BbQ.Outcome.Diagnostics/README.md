# BbQ.Outcome.Diagnostics

Optional adapters over `ILogger` and `System.Diagnostics.Activity`. The core Outcome package does not depend on this package, logging, or any telemetry exporter.

## Installation

This package ships with the Outcome family under the same `outcome-v*` version.

```bash
dotnet add package BbQ.Outcome.Diagnostics --prerelease
```

```csharp
using BbQ.Outcome.Diagnostics;
using System.Diagnostics;

using var source = new ActivitySource("MyApplication.Orders"); // normally application-owned
var observer = new OutcomeObserver<AppError>(logger);
var result = await observer.TraceAsync(source, "CreateOrder",
    ct => CreateOrderAsync(command, ct), cancellationToken);
```

`Observe` annotates the current activity and returns the original outcome. `TraceAsync` starts and disposes a child activity only when a listener requests it, and observes that child rather than accidentally changing a parent when no child is created. The caller owns the ActivitySource. Configure OpenTelemetry or another listener/exporter in the application.

Success records `outcome.success=true`, zero errors, and `Ok` activity status. Domain failure records false, an error count, and `Error` status without throwing. Operation cancellation is tagged as cancellation and rethrown without assigning an error status. Unexpected exceptions are rethrown with a generic activity status description and a generic log entry; their exception object, message, and stack are not recorded by this adapter.

Structured logs contain the caller-supplied operation name, success flag, and error count. They never automatically contain success values, error descriptions, metadata, attempted values, or error codes. Use stable, non-sensitive operation names. Activity error codes are opt-in via `OutcomeObservationOptions(IncludeErrorCodes: true)` and require an explicit `IErrorDescriptorProvider<TError>`. Code count/length is bounded, but applications must still control cardinality and sensitivity.

There is no implicit retry, exception-to-error conversion, best-effort exception swallowing, or global instrumentation switch. As with `Tap`, exceptions from application-supplied loggers or descriptor providers remain visible. Heterogeneous outcomes are supported with `OutcomeObserver<object?>` and the extension overloads.
