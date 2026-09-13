# BbQ.Cqrs sample scenarios

Run the sample application from the repository root:

```sh
dotnet run --project src/BbQ.Cqrs.Samples
```

`Program.cs` runs seven scenarios. The executable source is the authoritative complete example; snippets below show the key integration points rather than redefining its request/handler classes.

## 1. Basic query handler

`Scenario01_BasicQuery` sends `GetUserById` through `TestMediator<GetUserById, Outcome<UserDto>>` without behaviors. The handler reads `IUserRepository` and returns a successful DTO or a structured error.

```csharp
var mediator = new TestMediator<GetUserById, Outcome<UserDto>>(handler, []);
var outcome = await mediator.Send(new GetUserById("123"));
outcome.Switch(
    onSuccess: user => Console.WriteLine(user.Name),
    onError: errors => Console.WriteLine(string.Join("; ",
        errors.OfType<Error<AppError>>().Select(error => error.Description))));
```

The error list in `Outcome<T>` is heterogeneous. Filter to a known type before accessing type-specific properties, or use `Outcome<T,TError>` in a fully typed pipeline.

## 2. Command with validation

`Scenario02_CommandWithValidation` validates `RenameUser` before invoking the handler. The sample now uses the optional `BbQ.Cqrs.Outcome` package rather than a three-parameter behavior or a cast to an arbitrary response type.

```csharp
using BbQ.Cqrs.Validation;

var validator = new RenameUserValidator();
var factory = new OutcomeValidationFailureFactory<Unit>(
    new DelegateValidationIssueMapper<object?>(issue =>
        new Error<AppError>(AppError.InvalidName, issue.Message, ErrorSeverity.Validation)));
var validation = new ValidationBehavior<RenameUser, Outcome<Unit>>([validator], factory);
var mediator = new TestMediator<RenameUser, Outcome<Unit>>(handler, [validation]);

var invalid = await mediator.Send(new RenameUser("123", ""));
// invalid.IsError is true; the handler was not called.
var valid = await mediator.Send(new RenameUser("123", "Alice"));
// The valid command reaches the handler.
```

`RenameUserValidator` implements `IRequestValidator<RenameUser>` and returns ordered `ValidationIssue` objects. A nonempty issue list is converted into an Outcome failure; it does **not** throw `ValidationException` for ordinary invalid input. Validators run sequentially, support cancellation, and may share scoped dependencies. Unexpected exceptions remain exceptions.

For dependency injection, use closed registrations or the generated opt-in method:

```csharp
services.AddScoped<IRequestValidator<RenameUser>, RenameUserValidator>();
services.AddValidationIssueMapper<object?>(issue =>
    new Error<AppError>(AppError.InvalidName, issue.Message, ErrorSeverity.Validation));
services.AddOutcomeValidation<RenameUser, Unit>();
// Alternatively, use the generated AddBbQCqrsSamplesOutcomeValidation() method.
```

Register the mediator and handlers separately. Typed `Outcome<T,TError>` factories require an `IValidationIssueMapper<TError>`; arbitrary application response types can implement `IValidationFailureFactory<TResponse>`. See the [validation package README](../BbQ.Cqrs.Outcome/README.md) and [FluentValidation adapter](../BbQ.Cqrs.Outcome.FluentValidation/README.md).

## 3. Strongly typed error handling

`Scenario03_ErrorHandling` demonstrates generated enum helpers, heterogeneous error collections, filtering, and severity inspection.

```csharp
var outcome = Outcome<string>.FromErrors(new object?[]
{
    new Error<AppError>(AppError.UserNotFound, "User 123 not found"),
    new Error<AppError>(AppError.InvalidName, "Name invalid", ErrorSeverity.Validation),
    new Error<string>("OTHER", "Another error")
});
var appErrors = outcome.Errors.OfType<Error<AppError>>().ToList();
var validationErrors = appErrors.Where(error => error.Severity == ErrorSeverity.Validation).ToList();
```

The new core extension `MapError` can translate errors at an architectural boundary instead of discarding their type. `MapErrors` maps an entire failure list, and `TapError` observes it without changing the result. Generated catalogs additionally support stable external codes and localization resource keys; see [async composition and catalogs](../../docs/async-and-error-catalogs.md).

## 4. Retry behavior

`Scenario04_RetryBehavior` wraps a stub handler with the existing sample `RetryBehavior<GetUserById, Outcome<UserDto>, UserDto>`. It retries only the sample's transient error code, using configured attempt and delay limits.

```csharp
var retry = new RetryBehavior<GetUserById, Outcome<UserDto>, UserDto>(
    maxAttempts: 3, delay: TimeSpan.FromMilliseconds(100));
var mediator = new TestMediator<GetUserById, Outcome<UserDto>>(handler, [retry]);
var result = await mediator.Send(new GetUserById("42"));
```

The three-parameter retry behavior remains a manually registered sample, not an automatically registered two-parameter generic behavior. Retrying a state-changing operation requires an application-level idempotency policy. Ordinary Outcome `Map`/`Bind` do not introduce retries or catch unexpected exceptions.

## 5. Commands without a response payload

`Scenario05_FireAndForgetCommand` demonstrates `IRequest` and a nongeneric request handler for notifications. Although the scenario uses the traditional “fire-and-forget” label, awaiting mediator dispatch still waits for handler completion. It does not create a durable background job or guarantee delivery after process termination.

## 6. Separate command/query dispatchers

`Scenario06_DispatcherExample` invokes `DispatcherSample.RunExample`, showing `ICommandDispatcher` and `IQueryDispatcher` when applications prefer explicit command/query separation over a single mediator interface.

## 7. Pub/sub integration

`Scenario07_PubSubIntegration` invokes `PubSubIntegrationSample.RunAsync`, showing command-triggered event publishing, event handlers, stream subscribers, and streaming request handlers. The standalone Events package owns the bus/projection contracts.

## Testing the validation boundary

```csharp
[Test]
public async Task Invalid_name_returns_failure_without_invoking_handler()
{
    var handler = new StubHandler<RenameUser, Outcome<Unit>>(
        (_, _) => throw new InvalidOperationException("Handler must not run"));
    var factory = new OutcomeValidationFailureFactory<Unit>();
    var behavior = new ValidationBehavior<RenameUser, Outcome<Unit>>(
        [new RenameUserValidator()], factory);
    var mediator = new TestMediator<RenameUser, Outcome<Unit>>(handler, [behavior]);

    var result = await mediator.Send(new RenameUser("123", ""));
    Assert.That(result.IsError, Is.True);
    Assert.That(result.Errors.OfType<ValidationIssue>().Single().MemberName, Is.EqualTo("NewName"));
}
```

The integration suite also verifies generated registrations, field/code preservation, typed and custom response factories, cancellation, HTTP redaction, JSON round trips, and a full validation-to-HTTP flow. See `tests/BbQ.Outcome.Integrations.Tests`.

## Files and documentation

`Program.cs` contains the scenario orchestration. `ValidationBehavior.cs` now contains the sample `RenameUserValidator`; the reusable behavior and response factories live in `src/BbQ.Cqrs.Outcome`. Query/command handlers, retry behavior, dispatcher examples, and pub/sub examples remain separate source files.

- [CQRS documentation](../BbQ.Cqrs/README.md)
- [Outcome documentation](../BbQ.Outcome/README.md)
- [Extension implementation and migration](../../docs/extension-roadmap.md)
- [Validation integration](../BbQ.Cqrs.Outcome/README.md)
- [Events documentation](../BbQ.Events/README.md)
