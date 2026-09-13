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

## Registration

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
