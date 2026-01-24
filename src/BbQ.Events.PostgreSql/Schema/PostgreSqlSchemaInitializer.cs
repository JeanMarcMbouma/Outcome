using System.Reflection;
using System.Text;
using BbQ.Events.Schema;
using Npgsql;

namespace BbQ.Events.PostgreSql.Schema;

/// <summary>
/// Initializes the PostgreSQL database schema for event sourcing and projections.
/// </summary>
/// <remarks>
/// This initializer reads embedded SQL scripts and executes them in a transaction.
/// It is idempotent and safe to run multiple times - it will check for existing
/// tables before attempting to create them.
/// </remarks>
public class PostgreSqlSchemaInitializer : ISchemaInitializer
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the PostgreSqlSchemaInitializer class.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string</param>
    public PostgreSqlSchemaInitializer(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Ensures that the database schema exists, creating it if necessary.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Check and create tables in order
        await EnsureTableAsync(connection, "bbq_events", "Schema.CreateEventsTable.sql", cancellationToken);
        await EnsureTableAsync(connection, "bbq_streams", "Schema.CreateStreamsTable.sql", cancellationToken);
        await EnsureTableAsync(connection, "bbq_projection_checkpoints", "Schema.CreateCheckpointTable.sql", cancellationToken);
    }

    private async Task EnsureTableAsync(
        NpgsqlConnection connection,
        string tableName,
        string resourceName,
        CancellationToken cancellationToken)
    {
        // Check if table exists
        var checkSql = @"
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = 'public'
            AND table_name = @tableName";

        await using var checkCommand = new NpgsqlCommand(checkSql, connection);
        checkCommand.Parameters.AddWithValue("@tableName", tableName);

        var exists = Convert.ToInt64(await checkCommand.ExecuteScalarAsync(cancellationToken)) > 0;

        if (exists)
        {
            return; // Table already exists, skip creation
        }

        // Load and execute the SQL script
        var sql = LoadEmbeddedResource(resourceName);
        await using var createCommand = new NpgsqlCommand(sql, connection);
        await createCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private string LoadEmbeddedResource(string resourcePath)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"BbQ.Events.PostgreSql.{resourcePath.Replace("/", ".")}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Could not find embedded resource: {resourceName}");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
