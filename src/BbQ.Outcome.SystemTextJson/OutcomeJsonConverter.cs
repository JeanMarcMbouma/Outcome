using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace BbQ.Outcome.SystemTextJson;

/// <summary>A closed typed-outcome converter using explicitly supplied payload metadata.</summary>
public sealed class OutcomeJsonConverter<T, TError> : JsonConverter<Outcome<T, TError>>
{
    private readonly JsonTypeInfo<T> _valueInfo;
    private readonly JsonTypeInfo<TError> _errorInfo;
    private readonly OutcomeJsonEnvelope _envelope;

    public OutcomeJsonConverter(JsonTypeInfo<T> valueInfo, JsonTypeInfo<TError> errorInfo, OutcomeJsonEnvelope? envelope = null)
    {
        ArgumentNullException.ThrowIfNull(valueInfo);
        ArgumentNullException.ThrowIfNull(errorInfo);
        _valueInfo = valueInfo;
        _errorInfo = errorInfo;
        _envelope = envelope ?? new();
    }

    public override bool HandleNull => true;

    public override Outcome<T, TError> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var (isSuccess, payload) = _envelope.Read(document.RootElement);
        if (isSuccess)
            return Outcome<T, TError>.From(JsonSerializer.Deserialize(payload, _valueInfo)!);
        var errors = new TError[payload.GetArrayLength()];
        var index = 0;
        foreach (var element in payload.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Null)
                throw new JsonException("Failure errors cannot be null.");
            var error = JsonSerializer.Deserialize(element, _errorInfo);
            if (error is null)
                throw new JsonException("An error payload cannot deserialize to null.");
            errors[index++] = error;
        }
        return Outcome<T, TError>.FromErrors(errors);
    }

    public override void Write(Utf8JsonWriter writer, Outcome<T, TError> outcome, JsonSerializerOptions options)
    {
        // Validate the active branch before emitting the envelope (including default structs).
        var errors = outcome.IsSuccess ? null : outcome.Errors;
        writer.WriteStartObject();
        writer.WriteBoolean(_envelope.SuccessPropertyName, outcome.IsSuccess);
        if (outcome.IsSuccess)
        {
            writer.WritePropertyName(_envelope.ValuePropertyName);
            JsonSerializer.Serialize(writer, outcome.Value, _valueInfo);
        }
        else
        {
            writer.WritePropertyName(_envelope.ErrorsPropertyName);
            writer.WriteStartArray();
            foreach (var error in errors!)
                JsonSerializer.Serialize(writer, error, _errorInfo);
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }
}

/// <summary>A heterogeneous-outcome converter backed by an explicit error type registry.</summary>
public sealed class OutcomeJsonConverter<T> : JsonConverter<Outcome<T>>
{
    private readonly JsonTypeInfo<T> _valueInfo;
    private readonly OutcomeErrorTypeRegistry _registry;
    private readonly OutcomeJsonEnvelope _envelope;

    public OutcomeJsonConverter(JsonTypeInfo<T> valueInfo, OutcomeErrorTypeRegistry registry, OutcomeJsonEnvelope? envelope = null)
    {
        ArgumentNullException.ThrowIfNull(valueInfo);
        ArgumentNullException.ThrowIfNull(registry);
        _valueInfo = valueInfo;
        _registry = registry;
        _envelope = envelope ?? new();
    }

    public override bool HandleNull => true;

    public override Outcome<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var (isSuccess, payload) = _envelope.Read(document.RootElement);
        if (isSuccess)
            return Outcome<T>.From(JsonSerializer.Deserialize(payload, _valueInfo)!);
        var errors = new object?[payload.GetArrayLength()];
        var index = 0;
        foreach (var element in payload.EnumerateArray())
            errors[index++] = _registry.Read(element);
        return Outcome<T>.FromErrors(errors);
    }

    public override void Write(Utf8JsonWriter writer, Outcome<T> outcome, JsonSerializerOptions options)
    {
        var errors = outcome.IsSuccess ? null : outcome.Errors;
        if (errors is not null)
            foreach (var error in errors)
                _registry.EnsureSupported(error);
        writer.WriteStartObject();
        writer.WriteBoolean(_envelope.SuccessPropertyName, outcome.IsSuccess);
        if (outcome.IsSuccess)
        {
            writer.WritePropertyName(_envelope.ValuePropertyName);
            JsonSerializer.Serialize(writer, outcome.Value, _valueInfo);
        }
        else
        {
            writer.WritePropertyName(_envelope.ErrorsPropertyName);
            writer.WriteStartArray();
            foreach (var error in errors!)
                _registry.Write(writer, error!);
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }
}
