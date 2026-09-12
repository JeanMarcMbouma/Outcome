using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Collections.ObjectModel;

namespace BbQ.Outcome.AspNetCore;

/// <summary>Application-defined mapping for a complete failure, including mixed-error policy.</summary>
public interface IOutcomeHttpMapper<TError>
{
    ProblemDetails Map(IReadOnlyList<TError> errors);
}

/// <summary>Explicit exposure policy for one stable external error code.</summary>
public sealed record HttpErrorRule(int StatusCode, bool ExposeDescription = false,
    bool ExposeTarget = false, bool ExposeMetadata = false);

/// <summary>Configuration is snapshotted by the mapper; severity never selects an HTTP status.</summary>
public sealed class OutcomeHttpMappingOptions
{
    public IDictionary<string, HttpErrorRule> Rules { get; } = new Dictionary<string, HttpErrorRule>(StringComparer.Ordinal);
    public ISet<string> AllowedMetadataKeys { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Defaults to the common status when all agree; otherwise 500 if any server error exists,
    /// or 400 for mixed client errors. Custom policies must return a 400–599 status.
    /// </summary>
    public Func<IReadOnlyList<int>, int> SelectStatus { get; set; } = static statuses =>
        statuses.All(status => status == statuses[0]) ? statuses[0]
            : statuses.Any(status => status >= 500) ? 500 : 400;

    internal OutcomeHttpMappingOptions Snapshot()
    {
        ArgumentNullException.ThrowIfNull(SelectStatus);
        var copy = new OutcomeHttpMappingOptions { SelectStatus = SelectStatus };
        foreach (var pair in Rules)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                throw new ArgumentException("HTTP error codes must be nonblank.");
            ArgumentNullException.ThrowIfNull(pair.Value);
            ValidateStatus(pair.Value.StatusCode);
            copy.Rules.Add(pair.Key, pair.Value);
        }
        foreach (var key in AllowedMetadataKeys)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Allowed metadata keys must be nonblank.");
            copy.AllowedMetadataKeys.Add(key);
        }
        return copy;
    }

    internal static void ValidateStatus(int status)
    {
        if (status < 400 || status > 599)
            throw new ArgumentOutOfRangeException(nameof(status), "An error response must have a 400–599 status.");
    }
}

/// <summary>Only this redacted projection is included in Problem Details extensions.</summary>
public sealed record PublicOutcomeError(string Code, string Description, string? Target = null,
    IReadOnlyDictionary<string, object?>? Metadata = null);

/// <summary>Maps explicitly registered error codes and redacts all unapproved fields.</summary>
public sealed class OutcomeHttpMapper<TError> : IOutcomeHttpMapper<TError>
{
    private readonly IErrorDescriptorProvider<TError> _descriptors;
    private readonly OutcomeHttpMappingOptions _options;

    public OutcomeHttpMapper(IErrorDescriptorProvider<TError> descriptors, OutcomeHttpMappingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        _descriptors = descriptors;
        _options = (options ?? new OutcomeHttpMappingOptions()).Snapshot();
    }

    public ProblemDetails Map(IReadOnlyList<TError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
            throw new ArgumentException("An HTTP failure requires at least one error.", nameof(errors));
        var publicErrors = new PublicOutcomeError[errors.Count];
        var statuses = new int[errors.Count];
        for (var i = 0; i < errors.Count; i++)
        {
            ArgumentNullException.ThrowIfNull(errors[i]);
            var descriptor = _descriptors.Describe(errors[i])
                ?? throw new InvalidOperationException("An error descriptor provider returned null.");
            if (descriptor.Code is null || !_options.Rules.TryGetValue(descriptor.Code, out var rule))
            {
                statuses[i] = 500;
                publicErrors[i] = new("UNEXPECTED_ERROR", "An unexpected error occurred.");
                continue;
            }
            statuses[i] = rule.StatusCode;
            IReadOnlyDictionary<string, object?>? metadata = null;
            if (rule.ExposeMetadata && descriptor.Metadata is not null)
            {
                var approved = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var pair in descriptor.Metadata)
                    if (_options.AllowedMetadataKeys.Contains(pair.Key))
                        approved.Add(pair.Key, pair.Value);
                if (approved.Count > 0)
                    metadata = new ReadOnlyDictionary<string, object?>(approved);
            }
            publicErrors[i] = new(descriptor.Code,
                rule.ExposeDescription ? descriptor.Description : "The operation failed.",
                rule.ExposeTarget ? descriptor.Target : null, metadata);
        }
        var status = _options.SelectStatus(Array.AsReadOnly(statuses));
        OutcomeHttpMappingOptions.ValidateStatus(status);
        var title = ReasonPhrases.GetReasonPhrase(status);
        var problem = new ProblemDetails { Status = status, Title = string.IsNullOrEmpty(title) ? "Request failed." : title };
        problem.Extensions["errors"] = publicErrors;
        return problem;
    }
}
