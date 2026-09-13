using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace BbQ.Outcome.SystemTextJson;

/// <summary>Registers closed converters before <see cref="JsonSerializerOptions"/> is first used.</summary>
public static class OutcomeJsonExtensions
{
    /// <summary>Registers a closed converter for <see cref="Outcome{T,TError}"/> using explicit source-generated metadata.</summary>
    /// <example><code>
    /// var options = new JsonSerializerOptions()
    ///     .AddOutcomeConverter(
    ///         AppJsonContext.Default.User,
    ///         AppJsonContext.Default.AppError);
    ///
    /// var json = JsonSerializer.Serialize(result, options);
    /// </code></example>
    public static JsonSerializerOptions AddOutcomeConverter<T, TError>(this JsonSerializerOptions options,
        JsonTypeInfo<T> valueInfo, JsonTypeInfo<TError> errorInfo, OutcomeJsonEnvelope? envelope = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Converters.Add(new OutcomeJsonConverter<T, TError>(valueInfo, errorInfo, envelope));
        return options;
    }

    /// <summary>Registers a closed converter for heterogeneous <see cref="Outcome{T}"/> values using an explicit error-type registry.</summary>
    /// <example><code>
    /// var registry = new OutcomeErrorTypeRegistryBuilder()
    ///     .Register("validation", AppJsonContext.Default.ValidationError)
    ///     .Register("not-found", AppJsonContext.Default.NotFoundError)
    ///     .Build();
    ///
    /// var options = new JsonSerializerOptions()
    ///     .AddOutcomeConverter(AppJsonContext.Default.User, registry);
    /// </code></example>
    public static JsonSerializerOptions AddOutcomeConverter<T>(this JsonSerializerOptions options,
        JsonTypeInfo<T> valueInfo, OutcomeErrorTypeRegistry registry, OutcomeJsonEnvelope? envelope = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Converters.Add(new OutcomeJsonConverter<T>(valueInfo, registry, envelope));
        return options;
    }
}
