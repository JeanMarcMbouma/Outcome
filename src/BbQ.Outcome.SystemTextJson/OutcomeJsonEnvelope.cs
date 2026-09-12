using System.Text.Json;

namespace BbQ.Outcome.SystemTextJson;

/// <summary>
/// Explicit, case-sensitive wire names. Payload naming is controlled separately by its
/// JsonTypeInfo. Exactly one active branch is required, with no unknown or duplicate fields.
/// </summary>
public sealed class OutcomeJsonEnvelope
{
    public string SuccessPropertyName { get; }
    public string ValuePropertyName { get; }
    public string ErrorsPropertyName { get; }

    public OutcomeJsonEnvelope(string successPropertyName = "isSuccess", string valuePropertyName = "value",
        string errorsPropertyName = "errors")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(successPropertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(valuePropertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorsPropertyName);
        if (successPropertyName == valuePropertyName || successPropertyName == errorsPropertyName || valuePropertyName == errorsPropertyName)
            throw new ArgumentException("Outcome envelope property names must be distinct.");
        SuccessPropertyName = successPropertyName;
        ValuePropertyName = valuePropertyName;
        ErrorsPropertyName = errorsPropertyName;
    }

    internal (bool IsSuccess, JsonElement Payload) Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("An outcome must be a JSON object.");
        bool? success = null;
        JsonElement value = default, errors = default;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!seen.Add(property.Name))
                throw new JsonException("Duplicate outcome envelope property.");
            if (property.Name == SuccessPropertyName)
            {
                if (property.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    throw new JsonException("The outcome success flag must be boolean.");
                success = property.Value.GetBoolean();
            }
            else if (property.Name == ValuePropertyName)
                value = property.Value;
            else if (property.Name == ErrorsPropertyName)
                errors = property.Value;
            else
                throw new JsonException("Unknown outcome envelope property.");
        }
        if (success is null)
            throw new JsonException("The outcome success flag is required.");
        if (success.Value)
        {
            if (value.ValueKind == JsonValueKind.Undefined || errors.ValueKind != JsonValueKind.Undefined)
                throw new JsonException("A success requires a value property and must not contain errors.");
            return (true, value);
        }
        if (value.ValueKind != JsonValueKind.Undefined || errors.ValueKind != JsonValueKind.Array || errors.GetArrayLength() == 0)
            throw new JsonException("A failure requires a nonempty errors array and must not contain a value.");
        return (false, errors);
    }
}
