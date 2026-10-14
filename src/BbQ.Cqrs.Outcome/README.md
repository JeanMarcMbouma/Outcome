# BbQ.Cqrs.Outcome

Optional validation integration for BbQ.Cqrs and BbQ.Outcome, targeting .NET 8, 9, and 10. Neither base library depends on this package.

## Installation

This package ships with the CQRS family under the same `cqrs-v*` version.

```bash
dotnet add package BbQ.Cqrs.Outcome --prerelease
```

## Contracts

`IRequestValidator<TRequest>` returns an ordered list of `ValidationIssue(Code, Message, MemberName)`. An empty list is valid. `ValidationBehavior<TRequest,TResponse>` runs validators sequentially, accumulates their issues, and invokes the handler exactly once only when validation succeeds. Sequential execution permits shared scoped dependencies such as a DbContext. Exceptions and cancellation are not converted to validation issues.

`IValidationFailureFactory<TResponse>` creates the application's response. The behavior does not know its payload type and does not cast a concrete Outcome to an arbitrary response. `OutcomeValidationFailureFactory<T>` preserves issues as heterogeneous errors by default. `OutcomeValidationFailureFactory<T,TError>` uses an `IValidationIssueMapper<TError>`. Applications may implement the factory for other response types.

## Register mediator and validation together

Choose one discovery path. Both register scoped mediator services and command/query dispatchers,
sender/streamer aliases, handlers, request validators, and closed validation behaviors/factories
for `Outcome<T>` and `Outcome<T,TError>` requests. Invalid requests return failed Outcomes before
reaching their handlers. Neither path adds custom pipeline behaviors automatically.

### Assembly scanning

```csharp
services.AddBbQMediatorWithOutcomeValidationFromAssemblies(
    assemblies: [typeof(CreateUser).Assembly],
    configureValidation: validation => validation.MapIssuesTo<AppError>(issue =>
        new AppError(issue.Code, issue.Message, issue.MemberName)));
```

Supply the assemblies explicitly. Concrete closed request, handler and `IRequestValidator<TRequest>`
types are discovered, including internal types. Abstract/open generic types are skipped.
Handlers implementing multiple contracts are registered for each contract, including streaming
and fire-and-forget handlers. Streaming requests do not receive request validation behaviors.
The scanning API is annotated as requiring unreferenced code and dynamic code; use generated
registration for trimming-sensitive applications.

### Source-generated registration

Reference `BbQ.Cqrs.SourceGenerators` as an analyzer. For an assembly named `Ordering`:

```csharp
services.AddOrderingMediatorWithOutcomeValidation(
    configureValidation: validation => validation.MapIssuesTo<AppError>(issue =>
        new AppError(issue.Code, issue.Message, issue.MemberName)));
```

This single generated method registers mediator infrastructure, every accessible closed local handler,
and the validators/factories/behaviors emitted by `AddOrderingOutcomeValidation()`. No assembly scanning
or reflective registration is emitted. Call each participating assembly's generated method when
splitting handlers across projects. The method name uses the assembly name with punctuation removed.
Inaccessible and open generic types still require explicit registration. Generated registration is
suitable for Native AOT; mediator dispatch's existing runtime reflection is a separate concern.

### Defaults and domain mappings

Omit `configureValidation` for `Outcome<T>` (issues are preserved as heterogeneous errors) or
`Outcome<T,ValidationIssue>` (an identity mapper is registered automatically). Domain error types
require an explicit mapping once per error type, or a custom `IValidationFailureFactory<TResponse>`.
Enable `ServiceProviderOptions.ValidateOnBuild` to detect missing mapper dependencies at startup.
Mappings may also be registered after discovery, before building the service provider.

For stateful mapping with scoped dependencies:

```csharp
services.AddOrderingMediatorWithOutcomeValidation(
    configureValidation: validation => validation.MapIssuesTo<AppError, AppErrorMapper>());
```

`AppErrorMapper` implements `IValidationIssueMapper<AppError>`. Delegate mappers are singleton and
must be thread-safe. Explicit configuration replaces an earlier mapping for that error type.
Custom factories and mediator infrastructure registrations are preserved. Repeated calls, including
mixing scanning with generated validation registration, do not duplicate identical handlers,
validators, factories or validation behaviors. Discovered handlers/validators/behaviors are scoped.
Register application dependencies separately.

## Optional FluentValidation discovery

For `AbstractValidator<TRequest>`/`IValidator<TRequest>` discovery, use the optional
`BbQ.Cqrs.Outcome.FluentValidation` package's
`AddBbQMediatorWithOutcomeAndFluentValidationFromAssemblies` or generated
`Add{Assembly}MediatorWithOutcomeAndFluentValidation` companion. These compose the same setup
with scoped FluentValidation validators and asynchronous adapters. The native registration methods
remain unchanged. See the [FluentValidation guide](../BbQ.Cqrs.Outcome.FluentValidation/README.md).

## Explicit registration

```csharp
services.AddScoped<IRequestValidator<CreateUser>, CreateUserValidator>();
services.AddValidationIssueMapper<AppError>(issue => new AppError(issue.Code, issue.Message, issue.MemberName));
services.AddOutcomeValidation<CreateUser, User, AppError>();
```

The two-type-argument registration handles `Outcome<User>`; the three-type-argument registration handles `Outcome<User,AppError>`. They add closed, scoped registrations without reflection. Register the mediator and handler separately using existing CQRS APIs. Existing custom response factories are preserved. Validators and application-supplied mapper services can be scoped.

With `BbQ.Cqrs.SourceGenerators`, call `services.AddYourAssemblyNameOutcomeValidation()` to register closed behaviors/factories for all accessible nongeneric local requests that return Outcome. It also registers accessible local `IRequestValidator<TRequest>` implementations. This generated method is opt-in and emitted only when this optional integration is referenced. For arbitrary typed errors, supply the mapper yourself; the generator does not invent domain mappings. A typed `ValidationIssue` error gets an identity mapper. Open generic/inaccessible request and validator classes require explicit registration.

## Error descriptors

`ValidationIssueDescriptorProvider` exposes issue code/message/member through the optional core `IErrorDescriptorProvider<ValidationIssue>` contract. HTTP adapters still require an explicit policy about which descriptions and fields may be exposed.

## Compatibility

The core CQRS package remains independent of Outcome and retains its existing target frameworks. This package only supports .NET 8+. No validation is added automatically just by referencing it: use explicit or generated registrations. Registering twice does not duplicate an identical validation behavior.

