using BbQ.Cqrs;
using BbQ.Cqrs.Validation;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BbQ.Outcome.Integrations.Tests;

public sealed record ValidatedRequest(string Name) : ICommand<Outcome<int, ValidationIssue>>;
public sealed record HeterogeneousRequest : IQuery<Outcome<int>>;
public sealed record DomainRequest : IRequest<Outcome<int, string>>;

public sealed class NameValidator : IRequestValidator<ValidatedRequest>
{
    public Task<IReadOnlyList<ValidationIssue>> ValidateAsync(ValidatedRequest request, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ValidationIssue>>(string.IsNullOrEmpty(request.Name)
            ? new[] { new ValidationIssue("REQUIRED", "Name is required", nameof(request.Name)) }
            : Array.Empty<ValidationIssue>());
}

[TestFixture]
public sealed class ValidationIntegrationTests
{
    private sealed record Request : IRequest<Outcome<int, string>>;
    private sealed record CustomRequest : IRequest<CustomResponse>;
    private sealed record CustomResponse(IReadOnlyList<ValidationIssue> Issues);
    private sealed class CustomFactory : IValidationFailureFactory<CustomResponse>
    {
        public CustomResponse Create(IReadOnlyList<ValidationIssue> issues) => new(issues);
    }
    private sealed class Validator<T>(Func<T, CancellationToken, Task<IReadOnlyList<ValidationIssue>>> validate) : IRequestValidator<T>
    {
        public Task<IReadOnlyList<ValidationIssue>> ValidateAsync(T request, CancellationToken ct) => validate(request, ct);
    }
    private static OutcomeValidationFailureFactory<int, string> Factory()
        => new(new DelegateValidationIssueMapper<string>(issue => issue.Code));

    [Test]
    public async Task ValidatorsAreSequentialAndErrorsKeepRegistrationOrder()
    {
        var order = new List<string>();
        var first = new Validator<Request>(async (_, _) =>
        {
            order.Add("start1"); await Task.Yield(); order.Add("end1");
            return new[] { new ValidationIssue("A", "First"), new ValidationIssue("B", "Second") };
        });
        var second = new Validator<Request>((_, _) =>
        {
            order.Add("start2");
            return Task.FromResult<IReadOnlyList<ValidationIssue>>(new[] { new ValidationIssue("C", "Third") });
        });
        var behavior = new ValidationBehavior<Request, Outcome<int, string>>([first, second], Factory());
        var response = await behavior.Handle(new(), default, (_, _) => throw new AssertionException("Handler must be skipped"));
        Assert.That(response.Errors, Is.EqualTo(new[] { "A", "B", "C" }));
        Assert.That(order, Is.EqualTo(new[] { "start1", "end1", "start2" }));
    }

    [Test]
    public async Task EmptyValidatorsCallHandlerExactlyOnceWithOriginalToken()
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var behavior = new ValidationBehavior<Request, Outcome<int, string>>([], Factory());
        var result = await behavior.Handle(new(), cancellation.Token, (_, ct) =>
        {
            calls++;
            Assert.That(ct, Is.EqualTo(cancellation.Token));
            return Task.FromResult(Outcome<int, string>.From(42));
        });
        Assert.That(result.Value, Is.EqualTo(42));
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void CancellationAndUnexpectedExceptionsAreNotValidationFailures()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var behavior = new ValidationBehavior<Request, Outcome<int, string>>([], Factory());
        Assert.CatchAsync<OperationCanceledException>(async () => await behavior.Handle(new(), cancellation.Token,
            (_, _) => throw new AssertionException("Must not run")));
        var throwing = new Validator<Request>((_, _) => throw new InvalidOperationException("validator bug"));
        behavior = new([throwing], Factory());
        Assert.ThrowsAsync<InvalidOperationException>(async () => await behavior.Handle(new(), default,
            (_, _) => throw new AssertionException("Must not run")));
    }

    [Test]
    public void FactoriesRejectInvalidIssueListsAndNullMappedErrors()
    {
        Assert.Throws<ArgumentException>(() => Factory().Create(Array.Empty<ValidationIssue>()));
        Assert.Throws<ArgumentNullException>(() => Factory().Create(null!));
        Assert.Throws<ArgumentNullException>(() => Factory().Create(new ValidationIssue[] { null! }));
        Assert.Throws<ArgumentException>(() => Factory().Create(new[] { new ValidationIssue("", "Bad code") }));
        var nullMapper = new OutcomeValidationFailureFactory<int, string>(new DelegateValidationIssueMapper<string>(_ => null!));
        Assert.Throws<ArgumentException>(() => nullMapper.Create(new[] { new ValidationIssue("A", "First") }));
    }

    [Test]
    public async Task ApplicationSpecificResponseRequiresNoOutcomeCast()
    {
        var validator = new Validator<CustomRequest>((_, _) => Task.FromResult<IReadOnlyList<ValidationIssue>>(
            new[] { new ValidationIssue("CUSTOM", "Custom error", "Field") }));
        var behavior = new ValidationBehavior<CustomRequest, CustomResponse>([validator], new CustomFactory());
        var response = await behavior.Handle(new(), default, (_, _) => throw new AssertionException("Must skip"));
        Assert.That(response.Issues[0].Code, Is.EqualTo("CUSTOM"));
    }

    [Test]
    public void HeterogeneousFactoryPreservesFieldNamesAndCustomMapping()
    {
        var issue = new ValidationIssue("BAD", "Bad value", "Email");
        var response = new OutcomeValidationFailureFactory<int>().Create(new[] { issue });
        Assert.That(response.Errors[0], Is.SameAs(issue));
        var mapped = new OutcomeValidationFailureFactory<int>(new DelegateValidationIssueMapper<object?>(x => x.Code))
            .Create(new[] { issue });
        Assert.That(mapped.Errors[0], Is.EqualTo("BAD"));
        Assert.That(new ValidationIssueDescriptorProvider().Describe(issue).Target, Is.EqualTo("Email"));
    }

    [Test]
    public async Task GeneratedClosedRegistrationsResolveBothOutcomeFormsAndAreIdempotent()
    {
        var services = new ServiceCollection();
        services.AddValidationIssueMapper<string>(issue => issue.Code);
        services.AddBbQOutcomeIntegrationsTestsOutcomeValidation();
        services.AddBbQOutcomeIntegrationsTestsOutcomeValidation();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        var behaviors = scope.ServiceProvider.GetServices<IPipelineBehavior<ValidatedRequest, Outcome<int, ValidationIssue>>>().ToArray();
        Assert.That(behaviors.Length, Is.EqualTo(1));
        var failed = await behaviors[0].Handle(new(""), default, (_, _) => throw new AssertionException("Must skip"));
        Assert.That(failed.Errors[0].MemberName, Is.EqualTo("Name"));
        Assert.That(scope.ServiceProvider.GetRequiredService<IValidationFailureFactory<Outcome<int>>>(), Is.Not.Null);
        Assert.That(scope.ServiceProvider.GetRequiredService<IValidationFailureFactory<Outcome<int, string>>>(), Is.Not.Null);
    }

    [Test]
    public void ExplicitRegistrationPreservesExistingApplicationFactory()
    {
        var services = new ServiceCollection();
        var factory = new OutcomeValidationFailureFactory<int, string>(new DelegateValidationIssueMapper<string>(_ => "CUSTOM"));
        services.AddSingleton<IValidationFailureFactory<Outcome<int, string>>>(factory);
        services.AddOutcomeValidation<DomainRequest, int, string>();
        using var provider = services.BuildServiceProvider();
        Assert.That(provider.GetRequiredService<IValidationFailureFactory<Outcome<int, string>>>(), Is.SameAs(factory));
    }
}
