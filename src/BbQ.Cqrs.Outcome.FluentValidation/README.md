# BbQ.Cqrs.Outcome.FluentValidation

Optional FluentValidation 12 integration for .NET 8, 9, and 10. This dependency is not added to the base Outcome or CQRS packages.

## Installation

This package ships with the CQRS family under the same `cqrs-v*` version.

```bash
dotnet add package BbQ.Cqrs.Outcome.FluentValidation --prerelease
```

```csharp
services.AddBbQMediatorWithOutcomeAndFluentValidationFromAssemblies(
    assemblies: [typeof(CreateUser).Assembly],
    configureValidation: validation => validation.MapIssuesTo<AppError>(issue =>
        new AppError(issue.Code, issue.Message, issue.MemberName)));
```

This composed call registers scoped mediator services, handlers, native request validators,
FluentValidation `IValidator<TRequest>` implementations and Outcome validation. One closed
asynchronous adapter per request aggregates its FluentValidation validators. No separate
validator or adapter registration is required for discovered concrete closed validator classes.
The supplied assemblies must include both requests/handlers and the validators to discover.
Open generic validators require explicit registration.

## Source-generated setup

Reference `BbQ.Cqrs.SourceGenerators` as an analyzer. For an assembly named `Ordering`:

```csharp
services.AddOrderingMediatorWithOutcomeAndFluentValidation(
    configureValidation: validation => validation.MapIssuesTo<AppError>(issue =>
        new AppError(issue.Code, issue.Message, issue.MemberName)));
```

The method is emitted only when this optional FluentValidation adapter is referenced. It composes
generated mediator/Outcome setup with direct registrations for accessible closed local FluentValidation
validators and adapters. It emits no assembly scanning. Call each participating assembly's method
for multiple projects. Inaccessible/open generic validator classes require explicit registration.

Both paths also add adapters for discovered Outcome request types, so validators registered through
application factories can participate without a discoverable validator class. Native request validators
run first; the FluentValidation adapter follows and runs all registered FluentValidation validators
sequentially in deterministic type-name order for discovered classes. Repeated calls or mixing scanning,
generated and explicit adapter registration does not duplicate identical registrations.

Referencing the package alone does not enable discovery: the existing
`AddBbQMediatorWithOutcomeValidationFromAssemblies` and generated `AddOrderingMediatorWithOutcomeValidation`
methods retain their native-validator behavior. Choose the explicitly named FluentValidation method.

Omit configuration for `Outcome<T>` or `Outcome<T,ValidationIssue>`. Domain error types need an explicit
mapping or custom failure factory. `validation.MapIssuesTo<AppError, AppErrorMapper>()` supports scoped
mapping dependencies. Custom factories and registrations remain available, and application dependencies
must still be registered. The [Outcome registration guide](../BbQ.Cqrs.Outcome/README.md) covers defaults,
dependency validation and the distinction between generated registration and mediator dispatch reflection.

## Explicit registration

Existing per-request registration remains available:

```csharp
using BbQ.Cqrs.Validation.FluentValidation;
services.AddScoped<FluentValidation.IValidator<CreateUser>, CreateUserValidator>();
services.AddFluentValidationRequest<CreateUser>();
services.AddOutcomeValidation<CreateUser, User>();
```

The adapter runs `ValidateAsync(request, cancellationToken)` sequentially for each registered FluentValidation validator, so asynchronous rules are supported and shared scoped dependencies are not used concurrently. `ErrorCode`, `ErrorMessage`, and `PropertyName` become `ValidationIssue.Code`, `Message`, and `MemberName`. Cancellation and unexpected validator exceptions propagate. Attempted values and custom state are deliberately not copied into public errors.

For explicit per-request setup, register the response mapper (for arbitrary typed errors), handler,
and mediator separately. This adapter does not use ASP.NET's synchronous automatic-validation pipeline.

