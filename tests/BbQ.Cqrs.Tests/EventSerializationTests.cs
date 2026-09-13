using System.Text.Json;
using System.Text.Json.Serialization;
using BbQ.Events.Serialization;
using NUnit.Framework;

namespace BbQ.Cqrs.Tests;

public record VersionedEvent(string Name, int Count);
[JsonSerializable(typeof(VersionedEvent))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class EventTestJsonContext : JsonSerializerContext { }

[TestFixture]
public class EventSerializationTests
{
    private static JsonEventSerializer Create(int version = 1, params IEventUpcaster[] steps) =>
        new(new EventTypeRegistry().Register<VersionedEvent>("sample.changed", version, "Legacy.Sample"), steps);

    [Test]
    public void RoundTrip_UsesStableIdentityAndVersionedEnvelope()
    {
        var serializer = Create();
        var value = new VersionedEvent("Ada", 42);
        var encoded = serializer.Serialize(value);
        Assert.That(encoded.TypeId, Is.EqualTo("sample.changed"));
        Assert.That(encoded.Data, Does.Contain("\"schemaVersion\":1"));
        Assert.That(serializer.Deserialize<VersionedEvent>(encoded.TypeId, encoded.Data), Is.EqualTo(value));
    }

    [Test]
    public void GeneratedMetadata_RoundTrips()
    {
        var serializer = Create().RegisterMetadata(EventTestJsonContext.Default.VersionedEvent);
        var encoded = serializer.Serialize(new VersionedEvent("closed", 3));
        Assert.That(serializer.Deserialize<VersionedEvent>(encoded.TypeId, encoded.Data).Count, Is.EqualTo(3));
    }

    [Test]
    public void LegacyDefault_RemainsBareCamelCaseJson()
    {
        var serializer = new LegacyJsonEventSerializer();
        var encoded = serializer.Serialize(new VersionedEvent("Ada", 42));
        Assert.That(encoded.Data, Is.EqualTo("{\"name\":\"Ada\",\"count\":42}"));
        Assert.That(encoded.TypeId, Is.EqualTo(typeof(VersionedEvent).FullName));
        Assert.That(serializer.Deserialize<VersionedEvent>(encoded.TypeId, encoded.Data).Count, Is.EqualTo(42));
        Assert.Throws<InvalidOperationException>(() => serializer.Deserialize<VersionedEvent>("untrusted.Type, Assembly", encoded.Data));
    }

    [Test]
    public void HistoricalFixture_UpcastsInOrderWithoutChangingHistory()
    {
        const string fixture = "{\"name\":\"old\"}";
        var serializer = Create(3, new Step(1, payload => JsonSerializer.SerializeToElement(new { name = payload.GetProperty("name").GetString(), count = 1 })),
            new Step(2, payload => JsonSerializer.SerializeToElement(new { name = payload.GetProperty("name").GetString(), count = payload.GetProperty("count").GetInt32() + 1 })));
        Assert.That(serializer.Deserialize<VersionedEvent>("Legacy.Sample", fixture), Is.EqualTo(new VersionedEvent("old", 2)));
        Assert.That(serializer.Deserialize<VersionedEvent>("Legacy.Sample", fixture).Count, Is.EqualTo(2));
        Assert.That(fixture, Is.EqualTo("{\"name\":\"old\"}"));
    }

    [Test]
    public void MissingStep_FailsClosed() => Assert.Throws<InvalidOperationException>(() => Create(2).Deserialize<VersionedEvent>("Legacy.Sample", "{}"));
    [Test]
    public void AmbiguousStep_IsRejected() => Assert.Throws<ArgumentException>(() => Create(2, new Step(1, x => x), new Step(1, x => x)));
    [Test]
    public void UnknownIdentity_IsRejected() => Assert.Throws<InvalidOperationException>(() => Create().Deserialize<VersionedEvent>("System.Object, System.Private.CoreLib", "{}"));
    [Test]
    public void UnknownIdentity_CannotBeSilentlyFiltered() => Assert.Throws<InvalidOperationException>(() => Create().CanDeserialize<VersionedEvent>("unknown"));
    [Test]
    public void DuplicateAlias_IsRejected() => Assert.Throws<ArgumentException>(() => new EventTypeRegistry().Register<VersionedEvent>("same", 1, "same"));
    [TestCase("{")]
    [TestCase("{\"$eventEnvelope\":2,\"schemaVersion\":1,\"payload\":{}}")]
    [TestCase("{\"$eventEnvelope\":1,\"schemaVersion\":0,\"payload\":{}}")]
    [TestCase("{\"$eventEnvelope\":1,\"schemaVersion\":2,\"payload\":{}}")]
    [TestCase("{\"$eventEnvelope\":1,\"schemaVersion\":1,\"payload\":null}")]
    public void InvalidPayload_IsRejected(string data) => Assert.Catch(() => Create().Deserialize<VersionedEvent>("sample.changed", data));

    private sealed record Step(int FromVersion, Func<JsonElement, JsonElement> Transform) : IEventUpcaster
    {
        public string EventId => "sample.changed";
        public JsonElement Upcast(JsonElement payload) => Transform(payload);
    }
}
