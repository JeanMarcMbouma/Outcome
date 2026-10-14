using BbQ.Cqrs;
using BbQ.Cqrs.Validation;
using BbQ.Cqrs.Validation.FluentValidation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BbQ.Outcome.FluentValidation.Tests;

public sealed record EmailRequest(string Email) : ICommand<Outcome<int, ValidationIssue>>;
public sealed record DomainRequest(string Email) : IQuery<Outcome<int, string>>;
public sealed record MixedRequest(string Email) : ICommand<Outcome<int>>;
public sealed record ManualRequest(string Email) : ICommand<Outcome<int>>;

public sealed class ExecutionScope
{
    public Guid Id { get; } = Guid.NewGuid();
    public int HandlerCalls { get; set; }
    public int NativeCalls { get; set; }
    public List<string> RuleCalls { get; } = [];
    public List<CancellationToken> Tokens { get; } = [];
    public bool Active { get; private set; }
    public bool ThrowOnRule { get; set; }
    public async Task<bool> Check(string rule, bool valid, CancellationToken token)
    {
        Assert.That(Active, Is.False, "Scoped validators must run sequentially");
        Active = true;
        try
        {
            RuleCalls.Add(rule);
            Tokens.Add(token);
            await Task.Yield();
            token.ThrowIfCancellationRequested();
            if (ThrowOnRule) throw new InvalidOperationException("Rule failed unexpectedly");
            return valid;
        }
        finally { Active = false; }
    }
}

public sealed class FirstEmailValidator : AbstractValidator<EmailRequest>
{
    public FirstEmailValidator(ExecutionScope scope)
    {
        RuleFor(request => request.Email).MustAsync((value, token) => scope.Check("first", value.Contains('@'), token))
            .WithErrorCode("EMAIL").WithMessage("Email is invalid");
    }
}
public sealed class SecondEmailValidator : AbstractValidator<EmailRequest>
{
    public SecondEmailValidator(ExecutionScope scope)
    {
        RuleFor(request => request.Email).MustAsync((value, token) => scope.Check("second", value.EndsWith(".test"), token))
            .WithErrorCode("DOMAIN").WithMessage("Domain is invalid");
    }
}
public sealed class DomainValidator : AbstractValidator<DomainRequest>
{
    public DomainValidator() => RuleFor(request => request.Email).NotEmpty().WithErrorCode("REQUIRED");
}
public sealed class MixedValidator : AbstractValidator<MixedRequest>
{
    public MixedValidator() => RuleFor(request => request.Email).NotEmpty().WithErrorCode("REQUIRED");
}
public sealed class NativeValidator(ExecutionScope scope) : IRequestValidator<EmailRequest>
{
    public Task<IReadOnlyList<ValidationIssue>> ValidateAsync(EmailRequest request, CancellationToken ct)
    {
        scope.NativeCalls++;
        return Task.FromResult<IReadOnlyList<ValidationIssue>>(request.Email == "native-fail"
            ? new[] { new ValidationIssue("NATIVE", "Native validation failed", "Email") } : Array.Empty<ValidationIssue>());
    }
}
public sealed class Handlers(ExecutionScope scope) : IRequestHandler<EmailRequest, Outcome<int, ValidationIssue>>,
    IRequestHandler<DomainRequest, Outcome<int, string>>, IRequestHandler<MixedRequest, Outcome<int>>,
    IRequestHandler<ManualRequest, Outcome<int>>
{
    public Task<Outcome<int, ValidationIssue>> Handle(EmailRequest request, CancellationToken ct)
    {
        scope.HandlerCalls++;
        return Task.FromResult(Outcome<int, ValidationIssue>.From(42));
    }
    public Task<Outcome<int, string>> Handle(DomainRequest request, CancellationToken ct)
    {
        scope.HandlerCalls++;
        return Task.FromResult(Outcome<int, string>.From(42));
    }
    public Task<Outcome<int>> Handle(MixedRequest request, CancellationToken ct)
    {
        scope.HandlerCalls++;
        return Task.FromResult(Outcome<int>.From(42));
    }
    public Task<Outcome<int>> Handle(ManualRequest request, CancellationToken ct)
    {
        scope.HandlerCalls++;
        return Task.FromResult(Outcome<int>.From(42));
    }
}
public sealed class ScopedMapper(ExecutionScope scope) : IValidationIssueMapper<string>
{
    public string Map(ValidationIssue issue) => $"{issue.Code}:{scope.Id}";
}

[TestFixture]
public sealed class RegistrationTests
{
    private static IServiceCollection Register(IServiceCollection services, bool generated,
        Action<OutcomeValidationOptions>? configure = null)
        => generated ? services.AddBbQCqrsOutcomeFluentValidationTestsMediatorWithOutcomeAndFluentValidation(configure)
            : services.AddBbQMediatorWithOutcomeAndFluentValidationFromAssemblies([typeof(RegistrationTests).Assembly], configure);
    private static ServiceProvider Build(bool generated, Action<OutcomeValidationOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<ExecutionScope>();
        Register(services, generated, configure ?? (validation => validation.MapIssuesTo<string>(issue => issue.Code)));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task OneCallDiscoversAsyncValidatorsAndPreservesOutcomeForms(bool generated)
    {
        using var provider = Build(generated);
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var execution = scope.ServiceProvider.GetRequiredService<ExecutionScope>();
        var invalid = await mediator.Send(new EmailRequest("bad"), cancellation.Token);
        Assert.That(invalid.Errors.Select(issue => issue.Code), Is.EqualTo(new[] { "EMAIL", "DOMAIN" }));
        Assert.That(invalid.Errors[0], Is.EqualTo(new ValidationIssue("EMAIL", "Email is invalid", "Email")));
        Assert.That(execution.RuleCalls, Is.EqualTo(new[] { "first", "second" }));
        Assert.That(execution.Tokens, Is.All.EqualTo(cancellation.Token));
        Assert.That(execution.NativeCalls, Is.EqualTo(1));
        Assert.That((await mediator.Send(new DomainRequest(""))).Errors[0], Is.EqualTo("REQUIRED"));
        Assert.That((await mediator.Send(new MixedRequest(""))).Errors[0], Is.TypeOf<ValidationIssue>());
        Assert.That(execution.HandlerCalls, Is.Zero);
        Assert.That((await mediator.Send(new EmailRequest("a@example.test"))).Value, Is.EqualTo(42));
        Assert.That((await mediator.Send(new DomainRequest("valid"))).Value, Is.EqualTo(42));
        Assert.That((await mediator.Send(new MixedRequest("valid"))).Value, Is.EqualTo(42));
        Assert.That(execution.HandlerCalls, Is.EqualTo(3));
        Assert.That((await mediator.Send(new EmailRequest("native-fail"))).Errors[0].Code, Is.EqualTo("NATIVE"));
        Assert.That(execution.HandlerCalls, Is.EqualTo(3));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RepeatedMixedAndExplicitAdapterRegistrationAreIdempotent(bool generated)
    {
        var services = new ServiceCollection();
        services.AddScoped<ExecutionScope>();
        Register(services, generated, validation => validation.MapIssuesTo<string>(issue => issue.Code));
        Register(services, generated);
        Register(services, !generated);
        services.AddFluentValidationRequest<EmailRequest>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        Assert.That(sp.GetServices<IValidator<EmailRequest>>().Count(), Is.EqualTo(2));
        Assert.That(sp.GetServices<IRequestValidator<EmailRequest>>().Count(), Is.EqualTo(2));
        Assert.That(sp.GetServices<IPipelineBehavior<EmailRequest, Outcome<int, ValidationIssue>>>().Count(), Is.EqualTo(1));
        Assert.That(sp.GetServices<IRequestHandler<EmailRequest, Outcome<int, ValidationIssue>>>().Count(), Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ScopedMappingSharesValidationScope(bool generated)
    {
        using var provider = Build(generated, validation => validation.MapIssuesTo<string, ScopedMapper>());
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var execution = first.ServiceProvider.GetRequiredService<ExecutionScope>();
        Assert.That((await first.ServiceProvider.GetRequiredService<IMediator>().Send(new DomainRequest(""))).Errors[0],
            Is.EqualTo($"REQUIRED:{execution.Id}"));
        Assert.That(second.ServiceProvider.GetRequiredService<ExecutionScope>().Id, Is.Not.EqualTo(execution.Id));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CancellationAndRuleExceptionsPropagate(bool generated)
    {
        using var provider = Build(generated);
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var execution = scope.ServiceProvider.GetRequiredService<ExecutionScope>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () => await mediator.Send(new EmailRequest("bad"), cancellation.Token));
        Assert.That(execution.RuleCalls, Is.Empty);
        execution.ThrowOnRule = true;
        Assert.ThrowsAsync<InvalidOperationException>(async () => await mediator.Send(new EmailRequest("bad")));
        Assert.That(execution.HandlerCalls, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FluentValidationRemainsOptIn(bool generated)
    {
        var services = new ServiceCollection();
        services.AddScoped<ExecutionScope>();
        if (generated)
            services.AddBbQCqrsOutcomeFluentValidationTestsMediatorWithOutcomeValidation(validation => validation.MapIssuesTo<string>(issue => issue.Code));
        else
            services.AddBbQMediatorWithOutcomeValidationFromAssemblies([typeof(RegistrationTests).Assembly],
                validation => validation.MapIssuesTo<string>(issue => issue.Code));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.That(scope.ServiceProvider.GetServices<IValidator<EmailRequest>>(), Is.Empty);
        Assert.That(scope.ServiceProvider.GetServices<IRequestValidator<EmailRequest>>().Single(), Is.TypeOf<NativeValidator>());
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task FactoryRegisteredValidatorsNeedNoDiscoveredValidatorClass(bool generated)
    {
        var services = new ServiceCollection();
        services.AddScoped<ExecutionScope>();
        services.AddScoped<IValidator<ManualRequest>>(_ =>
        {
            var validator = new InlineValidator<ManualRequest>();
            validator.RuleFor(request => request.Email).NotEmpty().WithErrorCode("MANUAL");
            return validator;
        });
        Register(services, generated, validation => validation.MapIssuesTo<string>(issue => issue.Code));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new ManualRequest(""));
        Assert.That(((ValidationIssue)result.Errors[0]!).Code, Is.EqualTo("MANUAL"));
        Assert.That(scope.ServiceProvider.GetRequiredService<ExecutionScope>().HandlerCalls, Is.Zero);
    }
}
