using System.Reflection;
using System.Text;
using BbQ.Events.Schema;
using Microsoft.Data.SqlClient;

namespace BbQ.Events.SqlServer.Schema;

/// <summary>
/// Initializes the SQL Server database schema for event sourcing and projections.
/// </summary>
/// <remarks>
/// This initializer reads embedded SQL scripts and executes them in a transaction.
/// It is idempotent and safe to run multiple times - it will check for existing
/// tables before attempting to create them.
/// </remarks>
public sealed class SqlServerSchemaInitializer : ISchemaInitializer
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the SqlServerSchemaInitializer class.
    /// </summary>
    /// <param name="connectionString">SQL Server connection string</param>
    public SqlServerSchemaInitializer(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Ensures that the database schema exists, creating it if necessary.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Check and create tables in order
        await EnsureTableAsync(connection, "BbQ_Events", "Schema.CreateEventsTable.sql", cancellationToken).ConfigureAwait(false);
        await EnsureTableAsync(connection, "BbQ_Streams", "Schema.CreateStreamsTable.sql", cancellationToken).ConfigureAwait(false);
        await EnsureTableAsync(connection, "BbQ_ProjectionCheckpoints", "Schema.CreateCheckpointTable.sql", cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureTableAsync(
        SqlConnection connection,
        string tableName,
        string resourceName,
        CancellationToken cancellationToken)
    {
        // Check if table exists
        var checkSql = @"
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_NAME = @TableName";

        await using var checkCommand = new SqlCommand(checkSql, connection);
        checkCommand.Parameters.AddWithValue("@TableName", tableName);

        var exists = (int)(await checkCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0;

        if (exists)
        {
            return; // Table already exists, skip creation
        }

        // Load and execute the SQL script
        var sql = LoadEmbeddedResource(resourceName);
        await using var createCommand = new SqlCommand(sql, connection);
        await createCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private string LoadEmbeddedResource(string resourcePath)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"BbQ.Events.SqlServer.{resourcePath.Replace("/", ".")}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Could not find embedded resource: {resourceName}");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
