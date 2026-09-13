using BbQ.Cqrs;
using BbQ.Cqrs.Validation;
using BbQ.Outcome.AspNetCore;
using BbQ.Outcome.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Text.Json;

namespace BbQ.Outcome.Integrations.Tests;

[TestFixture]
public sealed class EndToEndExtensionTests
{
    [Test]
    public async Task GeneratedValidationToTypedErrorToJsonToRedactedHttpResponse()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddValidationIssueMapper<string>(issue => issue.Code);
        services.AddBbQOutcomeIntegrationsTestsOutcomeValidation();
        services.AddSingleton<IErrorDescriptorProvider<WireError>>(new DelegateErrorDescriptorProvider<WireError>(
            error => new(error.Code, error.Message, ErrorSeverity.Validation)));
        services.AddOutcomeHttpMapping<WireError>(options => options.Rules["REQUIRED"] = new(400));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        var behavior = scope.ServiceProvider.GetRequiredService<IPipelineBehavior<ValidatedRequest, Outcome<int, ValidationIssue>>>();
        var result = await behavior.Handle(new(""), default, (_, _) => throw new AssertionException("Invalid request must not reach handler"));
        var domain = result.MapError(issue => new WireError(issue.Code, issue.Message))
            .Map(value => new WireValue(value, "User"))
            .Observe(new OutcomeObserver<WireError>(), "ValidateUser");
        var wire = JsonSerializationTests.Context();
        var json = JsonSerializer.Serialize(domain, wire.TypedOutcome);
        var restored = JsonSerializer.Deserialize(json, wire.TypedOutcome);
        Assert.That(restored.Errors.Single().Code, Is.EqualTo("REQUIRED"));
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Response.Body = new MemoryStream();
        await restored.ToIResult(scope.ServiceProvider.GetRequiredService<IOutcomeHttpMapper<WireError>>()).ExecuteAsync(context);
        Assert.That(context.Response.StatusCode, Is.EqualTo(400));
        context.Response.Body.Position = 0;
        using var response = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.That(response.RootElement.GetRawText(), Does.Contain("REQUIRED"));
        Assert.That(response.RootElement.GetRawText(), Does.Not.Contain("Name is required"));
    }
}
