using BbQ.Outcome.SystemTextJson;
using NUnit.Framework;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BbQ.Outcome.Integrations.Tests;

public sealed record WireValue(int Id, string DisplayName);
public sealed record WireError(string Code, string Message);

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(WireValue))]
[JsonSerializable(typeof(WireError))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(Outcome<WireValue, WireError>), TypeInfoPropertyName = "TypedOutcome")]
[JsonSerializable(typeof(Outcome<WireValue>), TypeInfoPropertyName = "UntypedOutcome")]
[JsonSerializable(typeof(Outcome<string, WireError>), TypeInfoPropertyName = "NullableValueOutcome")]
internal partial class IntegrationJsonContext : JsonSerializerContext { }

[TestFixture]
public sealed class JsonSerializationTests
{
    internal static IntegrationJsonContext Context(OutcomeJsonEnvelope? envelope = null,
        OutcomeErrorTypeRegistry? registry = null)
    {
        var payload = IntegrationJsonContext.Default;
        registry ??= new OutcomeErrorTypeRegistryBuilder().Register("wire-v1", payload.WireError)
            .Register("text-v1", payload.String).Build();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.AddOutcomeConverter(payload.WireValue, payload.WireError, envelope);
        options.AddOutcomeConverter(payload.WireValue, registry, envelope);
        options.AddOutcomeConverter(payload.String, payload.WireError, envelope);
        return new IntegrationJsonContext(options);
    }

    [Test]
    public void TypedSuccessAndFailureRoundTripWithoutAccessingInactiveGetters()
    {
        var context = Context();
        var success = Outcome<WireValue, WireError>.From(new(7, "Name"));
        var json = JsonSerializer.Serialize(success, context.TypedOutcome);
        Assert.That(json, Is.EqualTo("{\"isSuccess\":true,\"value\":{\"id\":7,\"displayName\":\"Name\"}}"));
        Assert.That(JsonSerializer.Deserialize(json, context.TypedOutcome).Value, Is.EqualTo(success.Value));
        var failure = Outcome<WireValue, WireError>.FromErrors(new[] { new WireError("A", "First"), new WireError("B", "Second") });
        json = JsonSerializer.Serialize(failure, context.TypedOutcome);
        Assert.That(json, Does.Not.Contain("\"value\""));
        Assert.That(JsonSerializer.Deserialize(json, context.TypedOutcome).Errors, Is.EqualTo(failure.Errors));
    }

    [Test]
    public void NullSuccessAndHeterogeneousSuccessAreValid()
    {
        var context = Context();
        var nullSuccess = Outcome<string, WireError>.From(null!);
        var json = JsonSerializer.Serialize(nullSuccess, context.NullableValueOutcome);
        Assert.That(json, Is.EqualTo("{\"isSuccess\":true,\"value\":null}"));
        var restored = JsonSerializer.Deserialize(json, context.NullableValueOutcome);
        Assert.That(restored.IsSuccess, Is.True);
        Assert.That(restored.Value, Is.Null);
        var untyped = Outcome<WireValue>.From(new(1, "Name"));
        Assert.That(JsonSerializer.Deserialize(JsonSerializer.Serialize(untyped, context.UntypedOutcome), context.UntypedOutcome).Value,
            Is.EqualTo(untyped.Value));
    }

    [Test]
    public void HeterogeneousRegistryPreservesActualErrorTypesAndOrder()
    {
        var context = Context();
        var failure = Outcome<WireValue>.FromErrors(new object?[] { new WireError("A", "First"), "Second" });
        var json = JsonSerializer.Serialize(failure, context.UntypedOutcome);
        Assert.That(json, Does.Contain("\"type\":\"wire-v1\""));
        Assert.That(json, Does.Not.Contain("Assembly"));
        var restored = JsonSerializer.Deserialize(json, context.UntypedOutcome);
        Assert.That(restored.Errors[0], Is.TypeOf<WireError>().And.EqualTo(failure.Errors[0]));
        Assert.That(restored.Errors[1], Is.TypeOf<string>().And.EqualTo("Second"));
    }

    [TestCase("null")]
    [TestCase("[]")]
    [TestCase("{}")]
    [TestCase("{\"isSuccess\":1,\"value\":null}")]
    [TestCase("{\"isSuccess\":true}")]
    [TestCase("{\"isSuccess\":true,\"value\":null,\"errors\":[]}")]
    [TestCase("{\"isSuccess\":false,\"errors\":[],\"value\":null}")]
    [TestCase("{\"isSuccess\":false,\"errors\":[]}")]
    [TestCase("{\"isSuccess\":false,\"errors\":null}")]
    [TestCase("{\"isSuccess\":false,\"errors\":[null]}")]
    [TestCase("{\"isSuccess\":true,\"isSuccess\":true,\"value\":null}")]
    [TestCase("{\"isSuccess\":true,\"value\":null,\"value\":null}")]
    [TestCase("{\"isSuccess\":true,\"value\":null,\"extra\":1}")]
    [TestCase("{\"IsSuccess\":true,\"value\":null}")]
    public void MalformedEnvelopesAreRejectedForBothForms(string json)
    {
        var context = Context();
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json, context.TypedOutcome));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json, context.UntypedOutcome));
    }

    [TestCase("{\"type\":\"unknown\",\"error\":\"text\"}")]
    [TestCase("{\"type\":\"text-v1\",\"error\":null}")]
    [TestCase("{\"type\":\"text-v1\"}")]
    [TestCase("{\"error\":\"text\"}")]
    [TestCase("{\"type\":\"text-v1\",\"type\":\"text-v1\",\"error\":\"text\"}")]
    [TestCase("{\"type\":\"text-v1\",\"error\":\"text\",\"extra\":1}")]
    [TestCase("\"text\"")]
    public void MalformedOrUnregisteredHeterogeneousErrorsAreRejected(string error)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(
            "{\"isSuccess\":false,\"errors\":[" + error + "]}", Context().UntypedOutcome));
    }

    [Test]
    public void RegistryRejectsDuplicateIdsTypesAndUnregisteredRuntimeTypes()
    {
        var payload = IntegrationJsonContext.Default;
        var builder = new OutcomeErrorTypeRegistryBuilder().Register("wire-v1", payload.WireError);
        Assert.Throws<ArgumentException>(() => builder.Register("wire-v1", payload.String));
        Assert.Throws<ArgumentException>(() => builder.Register("wire-v2", payload.WireError));
        Assert.Throws<ArgumentException>(() => builder.Register(" ", payload.String));
        var snapshot = builder.Build();
        builder.Register("text-v1", payload.String);
        var oldContext = Context(registry: snapshot);
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(
            Outcome<WireValue>.FromErrors(new object?[] { "new type" }), oldContext.UntypedOutcome));
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(
            Outcome<WireValue>.FromErrors(new object?[] { 42 }), Context().UntypedOutcome));
    }

    [Test]
    public void CustomEnvelopeNamesAreExplicitAndPayloadNamingRemainsIndependent()
    {
        Assert.Throws<ArgumentException>(() => new OutcomeJsonEnvelope("same", "same", "errors"));
        var context = Context(new OutcomeJsonEnvelope("ok", "data", "problems"));
        var json = JsonSerializer.Serialize(Outcome<WireValue, WireError>.From(new(1, "Name")), context.TypedOutcome);
        Assert.That(json, Is.EqualTo("{\"ok\":true,\"data\":{\"id\":1,\"displayName\":\"Name\"}}"));
        Assert.That(JsonSerializer.Deserialize(json, context.TypedOutcome).Value.Id, Is.EqualTo(1));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("{\"isSuccess\":true,\"value\":null}", context.TypedOutcome));
    }

    [Test]
    public void UninitializedOutcomesCannotBeWritten()
    {
        var context = Context();
        Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(default(Outcome<WireValue, WireError>), context.TypedOutcome));
        Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(default(Outcome<WireValue>), context.UntypedOutcome));
    }
}
