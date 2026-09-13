using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace BbQ.Events.Serialization;

/// <summary>A provider-independent serialized event. Data is JSON; TypeId is never a CLR type to load.</summary>
public sealed record SerializedEvent(string TypeId, string Data);

/// <summary>Shared wire contract used by event stores and transports.</summary>
public interface IEventSerializer
{
    string GetTypeId(Type type);
    SerializedEvent Serialize<T>(T value);
    bool CanDeserialize<T>(string typeId);
    T Deserialize<T>(string typeId, string data);
}

/// <summary>An application-defined event identity and its current schema version.</summary>
public sealed record EventTypeIdentity(string Id, Type ClrType, int Version);

/// <summary>Resolves only application-registered identities; implementations must not load wire-supplied CLR names.</summary>
public interface IEventTypeResolver
{
    EventTypeIdentity Resolve(Type type);
    EventTypeIdentity Resolve(string id);
}

/// <summary>A closed registry. Legacy names are explicit aliases for version-one payloads.</summary>
public sealed class EventTypeRegistry : IEventTypeResolver
{
    private readonly Dictionary<Type, EventTypeIdentity> _types = new();
    private readonly Dictionary<string, EventTypeIdentity> _ids = new(StringComparer.Ordinal);

    public EventTypeRegistry Register<T>(string id, int version = 1, params string[] legacyNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (id.Length > 200 || id.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch is '.' or '-' or '_')))
            throw new ArgumentException("Use an ASCII event identity containing letters, digits, dots, hyphens or underscores (max 200).", nameof(id));
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        var names = legacyNames.Prepend(id).ToArray();
        if (_types.ContainsKey(typeof(T)) || names.Any(string.IsNullOrWhiteSpace) ||
            names.Distinct(StringComparer.Ordinal).Count() != names.Length || names.Any(_ids.ContainsKey))
            throw new ArgumentException("Duplicate or invalid event registration.");
        var identity = new EventTypeIdentity(id, typeof(T), version);
        _types.Add(typeof(T), identity);
        foreach (var name in names) _ids.Add(name, identity);
        return this;
    }

    public EventTypeIdentity Resolve(Type type) => _types.TryGetValue(type, out var identity)
        ? identity : throw new InvalidOperationException("Event type is not registered.");
    public EventTypeIdentity Resolve(string id) => _ids.TryGetValue(id, out var identity)
        ? identity : throw new InvalidOperationException("Unknown event identity.");
}

/// <summary>A deterministic, side-effect-free transformation from version N to N+1.</summary>
public interface IEventUpcaster
{
    string EventId { get; }
    int FromVersion { get; }
    JsonElement Upcast(JsonElement payload);
}

/// <summary>Compatibility serializer: original camel-case JSON and expected generic type's full name.</summary>
public sealed class LegacyJsonEventSerializer : IEventSerializer
{
    private readonly JsonSerializerOptions _options;
    public LegacyJsonEventSerializer(JsonSerializerOptions? options = null) =>
        _options = options ?? new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public string GetTypeId(Type type) => type.FullName ?? type.Name;
    public SerializedEvent Serialize<T>(T value) => new(typeof(T).FullName ?? typeof(T).Name, JsonSerializer.Serialize(value, _options));
    public bool CanDeserialize<T>(string typeId) => typeId == (typeof(T).FullName ?? typeof(T).Name);
    public T Deserialize<T>(string typeId, string data)
    {
        if (!CanDeserialize<T>(typeId)) throw new InvalidOperationException("Unexpected event identity.");
        return JsonSerializer.Deserialize<T>(data, _options) ?? throw new JsonException("Null event payload.");
    }
}

/// <summary>Versioned JSON envelopes with closed identity resolution and consecutive upcasting.</summary>
/// <remarks>Configure before sharing across threads. Register JsonTypeInfo for reflection-free payload serialization.</remarks>
public sealed class JsonEventSerializer : IEventSerializer
{
    private readonly IEventTypeResolver _resolver;
    private readonly JsonSerializerOptions _options;
    private readonly Dictionary<Type, JsonTypeInfo> _metadata = new();
    private readonly Dictionary<(string, int), IEventUpcaster> _upcasters = new();

    public JsonEventSerializer(IEventTypeResolver resolver, IEnumerable<IEventUpcaster>? upcasters = null, JsonSerializerOptions? options = null)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _options = options ?? new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        foreach (var step in upcasters ?? [])
        {
            var identity = resolver.Resolve(step.EventId);
            if (identity.Id != step.EventId || step.FromVersion < 1 || step.FromVersion >= identity.Version ||
                !_upcasters.TryAdd((step.EventId, step.FromVersion), step))
                throw new ArgumentException("Invalid or ambiguous upcasting step.", nameof(upcasters));
        }
    }

    public JsonEventSerializer RegisterMetadata<T>(JsonTypeInfo<T> metadata)
    {
        _metadata.Add(typeof(T), metadata);
        return this;
    }

    public string GetTypeId(Type type) => _resolver.Resolve(type).Id;

    public SerializedEvent Serialize<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var identity = _resolver.Resolve(typeof(T));
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("$eventEnvelope", 1);
            writer.WriteNumber("schemaVersion", identity.Version);
            writer.WritePropertyName("payload");
            if (_metadata.TryGetValue(typeof(T), out var metadata)) JsonSerializer.Serialize(writer, value, (JsonTypeInfo<T>)metadata);
            else JsonSerializer.Serialize(writer, value, _options);
            writer.WriteEndObject();
        }
        return new SerializedEvent(identity.Id, System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    public bool CanDeserialize<T>(string typeId) => _resolver.Resolve(typeId).ClrType == typeof(T);

    public T Deserialize<T>(string typeId, string data)
    {
        var identity = _resolver.Resolve(typeId);
        if (identity.ClrType != typeof(T)) throw new InvalidOperationException("Unexpected event identity.");
        using var document = JsonDocument.Parse(data);
        var payload = document.RootElement;
        var version = 1;
        // Canonical IDs always require an envelope. Only explicitly registered aliases read legacy bare JSON.
        if (typeId == identity.Id)
        {
            if (payload.GetProperty("$eventEnvelope").GetInt32() != 1) throw new JsonException("Unsupported envelope version.");
            version = payload.GetProperty("schemaVersion").GetInt32();
            payload = payload.GetProperty("payload");
        }
        if (version < 1 || version > identity.Version) throw new JsonException("Unsupported event schema version.");
        for (; version < identity.Version; version++)
        {
            if (!_upcasters.TryGetValue((identity.Id, version), out var step)) throw new InvalidOperationException("Missing upcasting step.");
            payload = step.Upcast(payload.Clone()).Clone();
        }
        var result = _metadata.TryGetValue(typeof(T), out var metadata)
            ? JsonSerializer.Deserialize(payload, (JsonTypeInfo<T>)metadata)
            : JsonSerializer.Deserialize<T>(payload, _options);
        return result ?? throw new JsonException("Null event payload.");
    }
}
