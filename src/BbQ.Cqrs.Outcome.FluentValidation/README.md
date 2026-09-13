# BbQ.Cqrs.Outcome.FluentValidation

Optional FluentValidation 12 integration for .NET 8, 9, and 10. This dependency is not added to the base Outcome or CQRS packages.

## Installation

This package ships with the CQRS family under the same `cqrs-v*` version.

```bash
dotnet add package BbQ.Cqrs.Outcome.FluentValidation --prerelease
```

```csharp
using BbQ.Cqrs.Validation.FluentValidation;

services.AddScoped<FluentValidation.IValidator<CreateUser>, CreateUserValidator>();
services.AddFluentValidationRequest<CreateUser>();
services.AddOutcomeValidation<CreateUser, User>();
```

The adapter runs `ValidateAsync(request, cancellationToken)` sequentially for each registered FluentValidation validator, so asynchronous rules are supported and shared scoped dependencies are not used concurrently. `ErrorCode`, `ErrorMessage`, and `PropertyName` become `ValidationIssue.Code`, `Message`, and `MemberName`. Cancellation and unexpected validator exceptions propagate. Attempted values and custom state are deliberately not copied into public errors.

Register the validator, response mapper (for arbitrary typed errors), handler, and mediator separately. This adapter does not use ASP.NET's synchronous automatic-validation pipeline.
