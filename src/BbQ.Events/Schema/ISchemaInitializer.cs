namespace BbQ.Events.Schema;

/// <summary>
/// Interface for database schema initialization.
/// </summary>
/// <remarks>
/// Implementations should be idempotent and safe to run multiple times.
/// They should check for existing schema objects before attempting to create them.
/// </remarks>
public interface ISchemaInitializer
{
    /// <summary>
    /// Ensures that the database schema exists, creating it if necessary.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <remarks>
    /// This method is idempotent and safe to call multiple times.
    /// It will check for existing schema objects and only create missing ones.
    /// </remarks>
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);
}
