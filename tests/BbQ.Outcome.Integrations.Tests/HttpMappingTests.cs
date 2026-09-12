using BbQ.Outcome.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Text.Json;

namespace BbQ.Outcome.Integrations.Tests;

[TestFixture]
public sealed class HttpMappingTests
{
    private static readonly IErrorDescriptorProvider<ErrorDescriptor> Descriptors =
        new DelegateErrorDescriptorProvider<ErrorDescriptor>(error => error);

    private static OutcomeHttpMappingOptions Options()
    {
        var options = new OutcomeHttpMappingOptions();
        options.Rules["MISSING"] = new(404, ExposeDescription: true);
        options.Rules["CONFLICT"] = new(409);
        options.Rules["INVALID"] = new(400, ExposeDescription: true, ExposeTarget: true);
        return options;
    }

    [TestCase("MISSING", 404)]
    [TestCase("CONFLICT", 409)]
    [TestCase("INVALID", 400)]
    [TestCase("UNREGISTERED_SECRET", 500)]
    public void StatusComesFromExplicitCodeNotSeverity(string code, int expected)
    {
        var mapper = new OutcomeHttpMapper<ErrorDescriptor>(Descriptors, Options());
        var problem = mapper.Map(new[] { new ErrorDescriptor(code, "Description", ErrorSeverity.Info) });
        Assert.That(problem.Status, Is.EqualTo(expected));
    }

    [Test]
    public void MixedErrorsHaveExplicitDefaultAndCustomPolicies()
    {
        var options = Options();
        var errors = new[] { new ErrorDescriptor("MISSING", "Missing"), new ErrorDescriptor("CONFLICT", "Conflict") };
        Assert.That(new OutcomeHttpMapper<ErrorDescriptor>(Descriptors, options).Map(errors).Status, Is.EqualTo(400));
        Assert.That(new OutcomeHttpMapper<ErrorDescriptor>(Descriptors, options).Map(
            errors.Concat(new[] { new ErrorDescriptor("UNKNOWN", "Private") }).ToArray()).Status, Is.EqualTo(500));
        options.SelectStatus = statuses => statuses.Max();
        Assert.That(new OutcomeHttpMapper<ErrorDescriptor>(Descriptors, options).Map(errors).Status, Is.EqualTo(409));
    }

    [Test]
    public void UnknownErrorsAreRedactedAndMetadataRequiresTwoOptIns()
    {
        var options = Options();
        options.Rules["APPROVED"] = new(400, ExposeDescription: true, ExposeTarget: true, ExposeMetadata: true);
        options.AllowedMetadataKeys.Add("limit");
        var metadata = new Dictionary<string, object?> { ["limit"] = 10, ["secret"] = "password" };
        var errors = new[]
        {
            new ErrorDescriptor("SECRET_CODE", "secret description", Target: "secret field", Metadata: metadata),
            new ErrorDescriptor("CONFLICT", "hidden conflict details", Target: "hidden field", Metadata: metadata),
            new ErrorDescriptor("APPROVED", "Safe description", Target: "Email", Metadata: metadata)
        };
        var problem = new OutcomeHttpMapper<ErrorDescriptor>(Descriptors, options).Map(errors);
        var exposed = (PublicOutcomeError[])problem.Extensions["errors"]!;
        Assert.That(exposed[0].Code, Is.EqualTo("UNEXPECTED_ERROR"));
        Assert.That(exposed[0].Description, Does.Not.Contain("secret"));
        Assert.That(exposed[0].Target, Is.Null);
        Assert.That(exposed[0].Metadata, Is.Null);
        Assert.That(exposed[1].Description, Does.Not.Contain("hidden"));
        Assert.That(exposed[1].Metadata, Is.Null);
        Assert.That(exposed[2].Description, Is.EqualTo("Safe description"));
        Assert.That(exposed[2].Target, Is.EqualTo("Email"));
        Assert.That(exposed[2].Metadata!.Keys, Is.EqualTo(new[] { "limit" }));
    }

    [Test]
    public void ConfigurationIsSnapshottedAndInvalidPoliciesAreRejected()
    {
        var options = Options();
        var mapper = new OutcomeHttpMapper<ErrorDescriptor>(Descriptors, options);
        options.Rules["MISSING"] = new(200);
        Assert.That(mapper.Map(new[] { new ErrorDescriptor("MISSING", "x") }).Status, Is.EqualTo(404));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OutcomeHttpMapper<ErrorDescriptor>(Descriptors, options));
        options = Options();
        options.SelectStatus = _ => 200;
        Assert.Throws<ArgumentOutOfRangeException>(() => new OutcomeHttpMapper<ErrorDescriptor>(Descriptors, options)
            .Map(new[] { new ErrorDescriptor("MISSING", "x") }));
        Assert.Throws<ArgumentException>(() => mapper.Map(Array.Empty<ErrorDescriptor>()));
        Assert.Throws<InvalidOperationException>(() => default(Outcome<int, ErrorDescriptor>).ToIResult(mapper));
    }

    [Test]
    public async Task HttpExecutionUsesProblemDetailsServiceAndCustomization()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["customized"] = true);
        services.AddSingleton(Descriptors);
        services.AddOutcomeHttpMapping<ErrorDescriptor>(options => options.Rules["MISSING"] = new(404));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Response.Body = new MemoryStream();
        context.Request.Headers.Accept = "application/problem+json";
        var outcome = Outcome<int, ErrorDescriptor>.FromError(new("MISSING", "private"));
        await outcome.ToIResult(scope.ServiceProvider.GetRequiredService<IOutcomeHttpMapper<ErrorDescriptor>>()).ExecuteAsync(context);
        Assert.That(context.Response.StatusCode, Is.EqualTo(404));
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.That(json.RootElement.GetProperty("customized").GetBoolean(), Is.True);
        Assert.That(json.RootElement.GetRawText(), Does.Not.Contain("private"));
    }

    [Test]
    public async Task SuccessCanChooseOkCreatedOrNoContent()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        using var provider = services.BuildServiceProvider();
        var mapper = new OutcomeHttpMapper<ErrorDescriptor>(Descriptors);
        var outcome = Outcome<int, ErrorDescriptor>.From(42);
        var results = new[] { outcome.ToIResult(mapper), outcome.ToIResult(mapper, x => TypedResults.Created("/items/42", x)),
            outcome.ToIResult(mapper, _ => TypedResults.NoContent()) };
        var statuses = new[] { 200, 201, 204 };
        for (var i = 0; i < results.Length; i++)
        {
            var context = new DefaultHttpContext { RequestServices = provider };
            context.Response.Body = new MemoryStream();
            await results[i].ExecuteAsync(context);
            Assert.That(context.Response.StatusCode, Is.EqualTo(statuses[i]));
            if (i == 1) Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo("/items/42"));
        }
    }

    [Test]
    public void AsyncResultConversionHonorsCancellationWhileWaiting()
    {
        var source = new TaskCompletionSource<Outcome<int, ErrorDescriptor>>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () => await source.Task.ToIResultAsync(
            new OutcomeHttpMapper<ErrorDescriptor>(Descriptors), cancellationToken: cancellation.Token));
        Assert.That(source.Task.IsCompleted, Is.False);
        source.SetResult(Outcome<int, ErrorDescriptor>.From(1));
    }
}
