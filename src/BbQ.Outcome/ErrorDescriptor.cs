namespace BbQ.Outcome;

/// <summary>
/// Optional transport-neutral projection of a domain error. Metadata must be explicitly
/// approved by the application before exposure; adapters may apply stricter redaction.
/// </summary>
public sealed record ErrorDescriptor(
    string Code,
    string Description,
    ErrorSeverity Severity = ErrorSeverity.Error,
    string? Target = null,
    IReadOnlyDictionary<string, object?>? Metadata = null,
    string? ResourceKey = null);

/// <summary>Projects arbitrary error types without imposing constraints on Outcome's TError.</summary>
public interface IErrorDescriptorProvider<in TError>
{
    /// <summary>Returns a transport-neutral descriptor for an error.</summary>
    ErrorDescriptor Describe(TError error);
}

/// <summary>Adapts an application-supplied descriptor function.</summary>
public sealed class DelegateErrorDescriptorProvider<TError> : IErrorDescriptorProvider<TError>
{
    private readonly Func<TError, ErrorDescriptor> _describe;

    /// <summary>Creates a provider. The function must return a non-null descriptor.</summary>
    public DelegateErrorDescriptorProvider(Func<TError, ErrorDescriptor> describe)
    {
        ArgumentNullException.ThrowIfNull(describe);
        _describe = describe;
    }

    /// <inheritdoc />
    public ErrorDescriptor Describe(TError error)
        => _describe(error) ?? throw new InvalidOperationException("An error descriptor provider returned null.");
}

/// <summary>Specifies a stable external error code for a generated enum member descriptor.</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class ErrorCodeAttribute(string code) : Attribute
{
    /// <summary>The stable, nonblank external code.</summary>
    public string Code { get; } = code;
}

/// <summary>Specifies a localization resource key without resolving a UI culture in the core.</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class ErrorResourceKeyAttribute(string key) : Attribute
{
    /// <summary>The nonblank localization resource key.</summary>
    public string Key { get; } = key;
}
