using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace BbQ.Outcome.SystemTextJson;

/// <summary>Registers closed converters before JsonSerializerOptions is first used.</summary>
public static class OutcomeJsonExtensions
{
    public static JsonSerializerOptions AddOutcomeConverter<T, TError>(this JsonSerializerOptions options,
        JsonTypeInfo<T> valueInfo, JsonTypeInfo<TError> errorInfo, OutcomeJsonEnvelope? envelope = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Converters.Add(new OutcomeJsonConverter<T, TError>(valueInfo, errorInfo, envelope));
        return options;
    }

    public static JsonSerializerOptions AddOutcomeConverter<T>(this JsonSerializerOptions options,
        JsonTypeInfo<T> valueInfo, OutcomeErrorTypeRegistry registry, OutcomeJsonEnvelope? envelope = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Converters.Add(new OutcomeJsonConverter<T>(valueInfo, registry, envelope));
        return options;
    }
}
