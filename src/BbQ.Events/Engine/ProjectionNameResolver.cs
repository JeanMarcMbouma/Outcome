namespace BbQ.Events.Engine;

/// <summary>
/// Provides consistent projection name resolution across all projection components.
/// </summary>
/// <remarks>
/// This resolver ensures that projection names are resolved using the same logic everywhere:
/// - DefaultProjectionEngine (runtime processing)
/// - DefaultReplayService (replay operations)
/// - DefaultProjectionRebuilder (rebuild operations)
/// 
/// Resolution logic:
/// 1. If ProjectionOptions.ProjectionName is set and not empty → use it
/// 2. Otherwise → use the projection type name
/// 
/// This consistency prevents mismatched projection identifiers between runtime processing
/// and replay/rebuild operations.
/// </remarks>
public static class ProjectionNameResolver
{
    /// <summary>
    /// Resolves the projection name for a given projection type and options.
    /// </summary>
    /// <param name="projectionType">The concrete type of the projection handler.</param>
    /// <param name="options">Optional projection options that may contain an explicit projection name.</param>
    /// <returns>The resolved projection name.</returns>
    public static string Resolve(Type projectionType, ProjectionOptions? options)
    {
        if (projectionType == null)
        {
            throw new ArgumentNullException(nameof(projectionType));
        }

        // If ProjectionOptions.ProjectionName is explicitly set, use it
        if (!string.IsNullOrWhiteSpace(options?.ProjectionName))
        {
            return options.ProjectionName;
        }

        // Otherwise, fall back to the projection type name
        return projectionType.Name;
    }
}
