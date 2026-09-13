using BbQ.Outcome;
using BbQ.Outcome.SystemTextJson;
using System.Text.Json;
using System.Text.Json.Serialization;

if (JsonSerializer.IsReflectionEnabledByDefault)
    throw new InvalidOperationException("This smoke test must execute with JSON reflection disabled.");

var payload = SmokeJsonContext.Default;
var registry = new OutcomeErrorTypeRegistryBuilder().Register("app-error-v1", payload.AppError)
    .Register("text-v1", payload.String).Build();
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
options.AddOutcomeConverter(payload.User, payload.AppError);
options.AddOutcomeConverter(payload.User, registry);
var wire = new SmokeJsonContext(options);

var success = Outcome<User, AppError>.From(new(42, "Ada"));
var successJson = JsonSerializer.Serialize(success, wire.TypedOutcome);
var restoredSuccess = JsonSerializer.Deserialize(successJson, wire.TypedOutcome);
Check(restoredSuccess.IsSuccess && restoredSuccess.Value == success.Value, "typed success");

var failure = Outcome<User, AppError>.FromErrors(new[] { new AppError("MISSING", "User missing"), new AppError("INVALID", "Invalid request") });
var failureJson = JsonSerializer.Serialize(failure, wire.TypedOutcome);
var restoredFailure = JsonSerializer.Deserialize(failureJson, wire.TypedOutcome);
Check(restoredFailure.IsError && restoredFailure.Errors.SequenceEqual(failure.Errors), "typed failure");

var untypedSuccess = Outcome<User>.From(new(7, "Grace"));
Check(JsonSerializer.Deserialize(JsonSerializer.Serialize(untypedSuccess, wire.UntypedOutcome), wire.UntypedOutcome).Value == untypedSuccess.Value,
    "heterogeneous success");
var heterogeneous = Outcome<User>.FromErrors(new object?[] { new AppError("MISSING", "User missing"), "Second error" });
var heterogeneousJson = JsonSerializer.Serialize(heterogeneous, wire.UntypedOutcome);
var restoredHeterogeneous = JsonSerializer.Deserialize(heterogeneousJson, wire.UntypedOutcome);
Check(restoredHeterogeneous.Errors[0] is AppError { Code: "MISSING" } && restoredHeterogeneous.Errors[1] is string text && text == "Second error",
    "heterogeneous typed errors");

var nullSuccess = Outcome<User, AppError>.From(null!);
Check(JsonSerializer.Deserialize(JsonSerializer.Serialize(nullSuccess, wire.TypedOutcome), wire.TypedOutcome).Value is null, "null success");
try
{
    JsonSerializer.Deserialize("{\"isSuccess\":false,\"errors\":[]}", wire.TypedOutcome);
    throw new InvalidOperationException("Malformed failure was accepted.");
}
catch (JsonException) { }

Console.WriteLine("AOT_SMOKE_PASS: typed and heterogeneous success/failure, runtime error types, null success, malformed input; reflection disabled.");

static void Check(bool condition, string scenario)
{
    if (!condition) throw new InvalidOperationException("AOT smoke failed: " + scenario);
}

public sealed record User(int Id, string Name);
public sealed record AppError(string Code, string Message);

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(User))]
[JsonSerializable(typeof(AppError))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(Outcome<User, AppError>), TypeInfoPropertyName = "TypedOutcome")]
[JsonSerializable(typeof(Outcome<User>), TypeInfoPropertyName = "UntypedOutcome")]
internal partial class SmokeJsonContext : JsonSerializerContext { }
