using BbQ.Cqrs.Validation;
using BbQ.Cqrs.Validation.FluentValidation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BbQ.Outcome.Integrations.Tests;

[TestFixture]
public sealed class FluentValidationIntegrationTests
{
    public sealed record Input(string Email);
    private sealed class AsyncValidator : AbstractValidator<Input>
    {
        public AsyncValidator(Action<CancellationToken> callback)
        {
            RuleFor(x => x.Email).MustAsync(async (email, ct) =>
            {
                callback(ct);
                await Task.Yield();
                return email.Contains('@');
            }).WithErrorCode("INVALID_EMAIL").WithMessage("Email is invalid");
        }
    }

    [Test]
    public async Task AsyncRulesPreserveCodeMessageAndMemberAndReceiveToken()
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var validator = new AsyncValidator(ct => { calls++; Assert.That(ct, Is.EqualTo(cancellation.Token)); });
        var adapter = new FluentValidationRequestValidator<Input>([validator]);
        var issues = await adapter.ValidateAsync(new("invalid"), cancellation.Token);
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(issues.Single(), Is.EqualTo(new ValidationIssue("INVALID_EMAIL", "Email is invalid", "Email")));
        Assert.That(await adapter.ValidateAsync(new("a@example.test"), cancellation.Token), Is.Empty);
    }

    [Test]
    public void CancelledValidationDoesNotRunRules()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var adapter = new FluentValidationRequestValidator<Input>([new AsyncValidator(_ => Assert.Fail("Must not run"))]);
        Assert.CatchAsync<OperationCanceledException>(async () => await adapter.ValidateAsync(new("bad"), cancellation.Token));
    }

    [Test]
    public void RegistrationIsClosedAndIdempotent()
    {
        var services = new ServiceCollection();
        services.AddScoped<IValidator<Input>>(_ => new AsyncValidator(_ => { }));
        services.AddFluentValidationRequest<Input>();
        services.AddFluentValidationRequest<Input>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        Assert.That(scope.ServiceProvider.GetServices<IRequestValidator<Input>>().Count(), Is.EqualTo(1));
    }
}
