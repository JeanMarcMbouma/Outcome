using BbQ.Events.Schema;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BbQ.Events.PostgreSql.Configuration;

/// <summary>
/// Hosted service that ensures the database schema is created on application startup.
/// </summary>
internal sealed class PostgreSqlSchemaInitializerHostedService : IHostedService
{
    private readonly ISchemaInitializer _schemaInitializer;
    private readonly ILogger<PostgreSqlSchemaInitializerHostedService> _logger;

    public PostgreSqlSchemaInitializerHostedService(
        ISchemaInitializer schemaInitializer,
        ILogger<PostgreSqlSchemaInitializerHostedService> logger)
    {
        _schemaInitializer = schemaInitializer ?? throw new ArgumentNullException(nameof(schemaInitializer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Ensuring PostgreSQL event store schema exists...");
            await _schemaInitializer.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("PostgreSQL event store schema initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize PostgreSQL event store schema");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
