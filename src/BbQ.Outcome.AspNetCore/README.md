# BbQ.Outcome.AspNetCore

Optional HTTP adapter for .NET 8, 9, and 10. The core Outcome package remains free of ASP.NET dependencies.

```csharp
using BbQ.Outcome.AspNetCore;

services.AddSingleton<IErrorDescriptorProvider<AppError>>(applicationErrorDescriptors);
services.AddOutcomeHttpMapping<AppError>(options =>
{
    options.Rules["USER_MISSING"] = new(404, ExposeDescription: true);
    options.Rules["EMAIL_CONFLICT"] = new(409, ExposeDescription: true);
    options.Rules["INVALID_EMAIL"] = new(400, ExposeDescription: true, ExposeTarget: true);
});

// Inside an endpoint; mapper comes from DI.
return outcome.ToIResult(mapper, user => TypedResults.Created($"/users/{user.Id}", user));
```

The default success result is `200 OK`; a success callback can choose `201 Created`, `204 No Content`, or any endpoint-specific result. Failures use `TypedResults.Problem(ProblemDetails)`, so the existing ASP.NET Core Problem Details service and its customization hooks remain in charge of response writing. Register `AddProblemDetails(options => ...)` for application-wide customization.

## Error mapping and exposure

`IOutcomeHttpMapper<TError>` can be replaced entirely. The supplied `OutcomeHttpMapper<TError>` obtains transport-neutral descriptors and applies an explicit code-to-status policy. **Severity does not determine HTTP status.** Unknown codes become a generic `500` with `UNEXPECTED_ERROR`; their original codes, descriptions, fields, and metadata are not exposed.

Known codes are exposed, but their descriptions, targets, and metadata remain hidden by default. Enable description/target exposure on an individual `HttpErrorRule`. Metadata requires both that rule's `ExposeMetadata` flag and an entry in `AllowedMetadataKeys`. Allowlisting a key approves its entire value, so avoid complex values containing secrets. Stack traces, exception objects, and attempted values are never copied automatically.

A failure has an `errors` extension containing redacted `PublicOutcomeError` entries in input order. When all mapped statuses agree, that status is used. Mixed statuses become `500` if any is a server error; otherwise `400`. Override `SelectStatus` for a different mixed-error policy. All configured and selected failure statuses must be 400–599.

Configuration dictionaries are snapshotted. Delegates are application-owned and should be thread-safe. Mapper services are scoped to allow request-scoped localization providers. Invalid/default outcomes are rejected through the core's guarded getters rather than serialized accidentally.

`ToIResultAsync` cancels waiting for an existing task but cannot cancel its producer; pass the request token to the producer too. This package is a boundary adapter, not a JSON wire format for Outcome itself.
