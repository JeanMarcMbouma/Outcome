using BbQ.Cqrs;
using BbQ.Outcome;
using BbQ.Cqrs.Validation;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BbQ.Cqrs.Outcome.Tests;

public sealed record ValidatedRequest(string Name) : ICommand<Outcome<int, ValidationIssue>>;
public sealed record DomainRequest : IRequest<Outcome<int, string>>;
public sealed class NameValidator : IRequestValidator<ValidatedRequest>
{
    public Task<IReadOnlyList<ValidationIssue>> ValidateAsync(ValidatedRequest request, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ValidationIssue>>(string.IsNullOrEmpty(request.Name)
            ? new[] { new ValidationIssue("REQUIRED", "Name is required", "Name") } : Array.Empty<ValidationIssue>());
}

public sealed class RegistrationExecution
{
    public int HandlerCalls { get; set; }
    public int ValidatorCalls { get; set; }
}

public sealed class RegisteredNameHandler(RegistrationExecution execution)
    : IRequestHandler<ValidatedRequest, Outcome<int, ValidationIssue>>
{
    public Task<Outcome<int, ValidationIssue>> Handle(ValidatedRequest request, CancellationToken ct)
    {
        execution.HandlerCalls++;
        return Task.FromResult(Outcome<int, ValidationIssue>.From(42));
    }
}

public sealed record RegistrationDomainRequest(string Name) : ICommand<Outcome<int, string>>;
public sealed record RegistrationMixedRequest(string Name) : IQuery<Outcome<int>>;

public sealed class RegistrationValidators(RegistrationExecution execution)
    : IRequestValidator<RegistrationDomainRequest>, IRequestValidator<RegistrationMixedRequest>
{
    public Task<IReadOnlyList<ValidationIssue>> ValidateAsync(RegistrationDomainRequest request, CancellationToken ct)
        => Validate(request.Name);
    public Task<IReadOnlyList<ValidationIssue>> ValidateAsync(RegistrationMixedRequest request, CancellationToken ct)
        => Validate(request.Name);
    private Task<IReadOnlyList<ValidationIssue>> Validate(string name)
    {
        execution.ValidatorCalls++;
        return Task.FromResult<IReadOnlyList<ValidationIssue>>(string.IsNullOrEmpty(name)
            ? new[] { new ValidationIssue("REQUIRED", "Name is required", "Name") } : Array.Empty<ValidationIssue>());
    }
}

// Multiple contracts on one class and direct IRequest are intentionally covered.
public sealed class RegistrationHandlers(RegistrationExecution execution)
    : IRequestHandler<RegistrationDomainRequest, Outcome<int, string>>,
      IRequestHandler<RegistrationMixedRequest, Outcome<int>>,
      IRequestHandler<DomainRequest, Outcome<int, string>>
{
    public Task<Outcome<int, string>> Handle(RegistrationDomainRequest request, CancellationToken ct)
    {
        execution.HandlerCalls++;
        return Task.FromResult(Outcome<int, string>.From(42));
    }
    public Task<Outcome<int>> Handle(RegistrationMixedRequest request, CancellationToken ct)
    {
        execution.HandlerCalls++;
        return Task.FromResult(Outcome<int>.From(42));
    }
    public Task<Outcome<int, string>> Handle(DomainRequest request, CancellationToken ct)
        => Task.FromResult(Outcome<int, string>.From(42));
}

public sealed class ScopedIssueMapper(RegistrationExecution execution) : IValidationIssueMapper<string>
{
    public string Map(ValidationIssue issue) => $"{issue.Code}:{execution.ValidatorCalls}";
}

[TestFixture]
public sealed class MediatorOutcomeRegistrationTests
{
    private static IServiceCollection Register(IServiceCollection services, bool generated,
        Action<OutcomeValidationOptions>? configure = null)
        => generated ? services.AddBbQCqrsOutcomeTestsMediatorWithOutcomeValidation(configure)
            : services.AddBbQMediatorWithOutcomeValidationFromAssemblies(
                [typeof(MediatorOutcomeRegistrationTests).Assembly], configure);

    private static ServiceProvider Build(bool generated, Action<OutcomeValidationOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<RegistrationExecution>();
        Register(services, generated, configure ?? (validation => validation.MapIssuesTo<string>(issue => issue.Code)));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task OneCallExecutesMediatorAndBlocksInvalidRequests(bool generated)
    {
        using var provider = Build(generated);
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var execution = scope.ServiceProvider.GetRequiredService<RegistrationExecution>();
        Assert.That(scope.ServiceProvider.GetRequiredService<ISender>(), Is.SameAs(mediator));
        Assert.That(scope.ServiceProvider.GetRequiredService<IStreamer>(), Is.SameAs(mediator));
        var identity = await mediator.Send(new ValidatedRequest(""));
        Assert.That(identity.Errors[0].MemberName, Is.EqualTo("Name"));
        var typed = await mediator.Send(new RegistrationDomainRequest(""));
        Assert.That(typed.Errors, Is.EqualTo(new[] { "REQUIRED" }));
        var mixed = await mediator.Send(new RegistrationMixedRequest(""));
        Assert.That(mixed.Errors[0], Is.TypeOf<ValidationIssue>());
        Assert.That(execution.HandlerCalls, Is.Zero);
        Assert.That((await mediator.Send(new ValidatedRequest("valid"))).Value, Is.EqualTo(42));
        Assert.That((await mediator.Send(new RegistrationDomainRequest("valid"))).Value, Is.EqualTo(42));
        Assert.That((await mediator.Send(new RegistrationMixedRequest("valid"))).Value, Is.EqualTo(42));
        Assert.That(execution.HandlerCalls, Is.EqualTo(3));
        Assert.That(execution.ValidatorCalls, Is.EqualTo(4));
        Assert.That(scope.ServiceProvider.GetRequiredService<IRequestHandler<DomainRequest, Outcome<int, string>>>(), Is.Not.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RepeatedAndMixedRegistrationDoesNotDuplicateComponents(bool generated)
    {
        var services = new ServiceCollection();
        services.AddScoped<RegistrationExecution>();
        services.AddValidationIssueMapper<string>(issue => issue.Code);
        Register(services, generated);
        Register(services, generated);
        Register(services, !generated);
        services.AddBbQCqrsOutcomeTestsOutcomeValidation();
        services.AddOutcomeValidation<ValidatedRequest, int, ValidationIssue>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        Assert.That(sp.GetServices<IMediator>().Count(), Is.EqualTo(1));
        Assert.That(sp.GetServices<IRequestHandler<ValidatedRequest, Outcome<int, ValidationIssue>>>().Count(), Is.EqualTo(1));
        Assert.That(sp.GetServices<IRequestValidator<ValidatedRequest>>().Count(), Is.EqualTo(1));
        Assert.That(sp.GetServices<IPipelineBehavior<ValidatedRequest, Outcome<int, ValidationIssue>>>().Count(), Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ScopedMapperUsesTheValidatorsScope(bool generated)
    {
        using var provider = Build(generated, validation => validation.MapIssuesTo<string, ScopedIssueMapper>());
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var result = await first.ServiceProvider.GetRequiredService<IMediator>().Send(new RegistrationDomainRequest(""));
        Assert.That(result.Errors[0], Is.EqualTo("REQUIRED:1"));
        Assert.That(second.ServiceProvider.GetRequiredService<RegistrationExecution>().ValidatorCalls, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CustomFactoryIsPreserved(bool generated)
    {
        var services = new ServiceCollection();
        var factory = new OutcomeValidationFailureFactory<int, string>(new DelegateValidationIssueMapper<string>(_ => "CUSTOM"));
        services.AddSingleton<IValidationFailureFactory<Outcome<int, string>>>(factory);
        services.AddScoped<RegistrationExecution>();
        Register(services, generated);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        Assert.That(provider.GetRequiredService<IValidationFailureFactory<Outcome<int, string>>>(), Is.SameAs(factory));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MissingDomainMappingFailsDependencyValidation(bool generated)
    {
        var services = new ServiceCollection();
        services.AddScoped<RegistrationExecution>();
        Register(services, generated);
        var error = Assert.Throws<AggregateException>(() => services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true }));
        Assert.That(error!.ToString(), Does.Contain("IValidationIssueMapper"));
    }

    [Test]
    public void ScanningRequiresExplicitNonNullAssemblies()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => services.AddBbQMediatorWithOutcomeValidationFromAssemblies(null!));
        Assert.Throws<ArgumentException>(() => services.AddBbQMediatorWithOutcomeValidationFromAssemblies([]));
        Assert.Throws<ArgumentException>(() => services.AddBbQMediatorWithOutcomeValidationFromAssemblies([null!]));
    }
}
