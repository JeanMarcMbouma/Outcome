using BbQ.Events.Events;
using BbQ.Events.PostgreSql.Schema;

namespace BbQ.Events.PostgreSql.Events;

/// <summary>
/// Extension methods for PostgreSQL event store.
/// </summary>
public static class PostgreSqlEventStoreExtensions
{
    /// <summary>
    /// Ensures the PostgreSQL event store schema exists, creating it if necessary.
    /// </summary>
    /// <param name="eventStore">The event store instance</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <remarks>
    /// This method is idempotent and safe to call multiple times.
    /// It will check for existing schema objects and only create missing ones.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if eventStore is null</exception>
    /// <exception cref="InvalidOperationException">Thrown if the event store is not a PostgreSqlEventStore</exception>
    public static async Task EnsureSchemaAsync(
        this IEventStore eventStore,
        CancellationToken cancellationToken = default)
    {
        if (eventStore == null)
        {
            throw new ArgumentNullException(nameof(eventStore));
        }

        if (eventStore is not PostgreSqlEventStore postgreSqlEventStore)
        {
            throw new InvalidOperationException(
                "EnsureSchemaAsync can only be called on PostgreSqlEventStore instances. " +
                "Use the appropriate extension method for your event store implementation.");
        }

        var initializer = new PostgreSqlSchemaInitializer(postgreSqlEventStore.ConnectionString);
        await initializer.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
    }
}
