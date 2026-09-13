using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace BbQ.Outcome.SystemTextJson;

/// <summary>
/// Builds an explicit allowlist for heterogeneous error round trips. Identifiers are stable
/// application contracts, not CLR type names. Build before concurrent serialization begins.
/// </summary>
/// <example><code>
/// var registry = new OutcomeErrorTypeRegistryBuilder()
///     .Register("validation", AppJsonContext.Default.ValidationError)
///     .Register("not-found", AppJsonContext.Default.NotFoundError)
///     .Build();
/// </code></example>
public sealed class OutcomeErrorTypeRegistryBuilder
{
    private readonly Dictionary<string, ErrorTypeEntry> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, ErrorTypeEntry> _byType = [];

    /// <summary>Registers one exact runtime error type under a stable external identifier.</summary>
    /// <example><code>
    /// builder.Register("validation", AppJsonContext.Default.ValidationError);
    /// </code></example>
    public OutcomeErrorTypeRegistryBuilder Register<TError>(string id, JsonTypeInfo<TError> typeInfo) where TError : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(typeInfo);
        if (_byId.ContainsKey(id) || _byType.ContainsKey(typeof(TError)))
            throw new ArgumentException("Each error identifier and runtime type can be registered only once.", nameof(id));
        var entry = new ErrorTypeEntry<TError>(id, typeInfo);
        _byId.Add(id, entry);
        _byType.Add(typeof(TError), entry);
        return this;
    }

    /// <summary>Creates an immutable snapshot unaffected by subsequent registrations on this builder.</summary>
    /// <example><code>
    /// var registry = builder.Build();
    /// options.AddOutcomeConverter(AppJsonContext.Default.User, registry);
    /// </code></example>
    public OutcomeErrorTypeRegistry Build() => new(_byId, _byType);
}

/// <summary>
/// Immutable registry using exact runtime-type matches and no dynamic type loading.
/// Pass an instance to the heterogeneous <c>AddOutcomeConverter</c> overload.
/// </summary>
/// <example><code>
/// var registry = new OutcomeErrorTypeRegistryBuilder()
///     .Register("validation", AppJsonContext.Default.ValidationError)
///     .Build();
///
/// var options = new JsonSerializerOptions()
///     .AddOutcomeConverter(AppJsonContext.Default.User, registry);
/// </code></example>
public sealed class OutcomeErrorTypeRegistry
{
    private readonly Dictionary<string, ErrorTypeEntry> _byId;
    private readonly Dictionary<Type, ErrorTypeEntry> _byType;

    internal OutcomeErrorTypeRegistry(Dictionary<string, ErrorTypeEntry> byId, Dictionary<Type, ErrorTypeEntry> byType)
    {
        _byId = new(byId, StringComparer.Ordinal);
        _byType = new(byType);
    }

    internal object Read(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new JsonException("A heterogeneous error requires a type identifier and error payload.");
        string? id = null;
        JsonElement payload = default;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!seen.Add(property.Name))
                throw new JsonException("Duplicate heterogeneous error property.");
            switch (property.Name)
            {
                case "type":
                    if (property.Value.ValueKind != JsonValueKind.String)
                        throw new JsonException("An error type identifier must be a string.");
                    id = property.Value.GetString();
                    break;
                case "error": payload = property.Value; break;
                default: throw new JsonException("Unknown heterogeneous error property.");
            }
        }
        if (id is null || !_byId.TryGetValue(id, out var entry))
            throw new JsonException("The error type identifier is not registered.");
        if (payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            throw new JsonException("A non-null error payload is required.");
        return entry.Read(payload);
    }

    internal void EnsureSupported(object? error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (!_byType.ContainsKey(error.GetType()))
            throw new NotSupportedException("The error's exact runtime type has not been registered.");
    }

    internal void Write(Utf8JsonWriter writer, object error)
    {
        EnsureSupported(error);
        var entry = _byType[error.GetType()];
        writer.WriteStartObject();
        writer.WriteString("type", entry.Id);
        writer.WritePropertyName("error");
        entry.Write(writer, error);
        writer.WriteEndObject();
    }
}

internal abstract class ErrorTypeEntry(string id)
{
    internal string Id { get; } = id;
    internal abstract object Read(JsonElement payload);
    internal abstract void Write(Utf8JsonWriter writer, object error);
}

internal sealed class ErrorTypeEntry<TError>(string id, JsonTypeInfo<TError> typeInfo) : ErrorTypeEntry(id) where TError : notnull
{
    internal override object Read(JsonElement payload)
    {
        var error = JsonSerializer.Deserialize(payload, typeInfo);
        return error is null ? throw new JsonException("An error payload cannot deserialize to null.") : error;
    }

    internal override void Write(Utf8JsonWriter writer, object error)
        => JsonSerializer.Serialize(writer, (TError)error, typeInfo);
}
