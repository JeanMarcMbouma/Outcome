using BbQ.Events.RabbitMQ.Configuration;
using System.Text.Json;
using BbQ.Events.Engine;
using BbQ.Events.Events;
using BbQ.Events.Serialization;
using BbQ.Events.SqlServer.Events;
using BbQ.Events.SqlServer.Schema;
using BbQ.Events.PostgreSql.Events;
using BbQ.Events.PostgreSql.Schema;
using BbQ.Events.RabbitMQ.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BbQ.Events.ProviderIntegration.Tests;

[TestFixture]
public class DurableProviderTests
{
    public record Changed(string Id, string Name);
    private static JsonEventSerializer Serializer() => new(new EventTypeRegistry().Register<Changed>("sample.changed", 3, typeof(Changed).FullName!), [new ChangeUpcaster(1), new ChangeUpcaster(2)]);
    private sealed record ChangeUpcaster(int FromVersion) : IEventUpcaster
    {
        public string EventId => "sample.changed";
        public JsonElement Upcast(JsonElement payload) => JsonSerializer.SerializeToElement(new
        { id = payload.GetProperty("id").GetString(), name = payload.GetProperty("name").GetString() + "-v" + (FromVersion + 1) });
    }
    private static string Require(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrEmpty(value))
        {
            if (Environment.GetEnvironmentVariable("REQUIRE_EVENT_SERVICES") == "1") Assert.Fail($"Missing required service: {variable}");
            Assert.Ignore($"Service not configured: {variable}");
        }
        return value!;
    }
    private static async Task<IEventStore> Store(string provider, IEventSerializer serializer)
    {
        if (provider == "sql")
        {
            var connection = Require("TEST_SQLSERVER_CONNECTION_STRING");
            await new SqlServerSchemaInitializer(connection).EnsureSchemaAsync();
            return new SqlServerEventStore(new() { ConnectionString = connection, EventSerializer = serializer });
        }
        var pg = Require("TEST_POSTGRESQL_CONNECTION_STRING");
        await new PostgreSqlSchemaInitializer(pg).EnsureSchemaAsync();
        return new PostgreSqlEventStore(new() { ConnectionString = pg, EventSerializer = serializer });
    }
    [TestCase("sql")]
    [TestCase("postgres")]
    public async Task StoredHistory_LegacyAndVersionedEventsReplayTogether(string provider)
    {
        var stream = "issue64-" + Guid.NewGuid();
        var legacy = await Store(provider, new LegacyJsonEventSerializer());
        await legacy.AppendAsync(stream, new Changed("1", "historical"));
        var modern = await Store(provider, Serializer());
        await modern.AppendAsync(stream, new Changed("2", "current"));
        var events = new List<StoredEvent<Changed>>();
        await foreach (var item in modern.ReadAsync<Changed>(stream)) events.Add(item);
        Assert.That(events.Select(e => e.Event.Name), Is.EqualTo(new[] { "historical-v2-v3", "current" }));
        Assert.That(events[1].Position, Is.GreaterThan(events[0].Position));
    }
    [Test]
    public async Task HistoricalWireData_InterchangesBetweenSqlServerAndPostgreSql()
    {
        var sqlConnection = Require("TEST_SQLSERVER_CONNECTION_STRING");
        var pgConnection = Require("TEST_POSTGRESQL_CONNECTION_STRING");
        var stream = "interchange-" + Guid.NewGuid();
        var sql = await Store("sql", new LegacyJsonEventSerializer());
        var pg = await Store("postgres", Serializer());
        await sql.AppendAsync(stream, new Changed("wire-1", "historical"));
        await using var source = new Microsoft.Data.SqlClient.SqlConnection(sqlConnection);
        await source.OpenAsync();
        await using var read = source.CreateCommand();
        read.CommandText = "SELECT EventType, EventData FROM BbQ_Events WHERE StreamName = @stream";
        read.Parameters.AddWithValue("@stream", stream);
        await using var reader = await read.ExecuteReaderAsync();
        Assert.That(await reader.ReadAsync(), Is.True);
        await using var destination = new Npgsql.NpgsqlConnection(pgConnection);
        await destination.OpenAsync();
        await using var insert = destination.CreateCommand();
        insert.CommandText = "INSERT INTO bbq_events (stream_name, position, event_type, event_data) VALUES (@stream, 0, @type, @data)";
        insert.Parameters.AddWithValue("@stream", stream);
        insert.Parameters.AddWithValue("@type", reader.GetString(0));
        insert.Parameters.AddWithValue("@data", reader.GetString(1));
        await insert.ExecuteNonQueryAsync();
        var decoded = new List<Changed>();
        await foreach (var item in pg.ReadAsync<Changed>(stream)) decoded.Add(item.Event);
        Assert.That(decoded, Is.EqualTo(new[] { new Changed("wire-1", "historical-v2-v3") }));
    }
    [Test]
    public async Task SqlQuarantine_RestartDeduplicatesAndReplaysThroughSharedSerializer()
    {
        var connection = Require("TEST_SQLSERVER_CONNECTION_STRING");
        var store = new SqlServerProjectionDeadLetterStore(connection);
        await store.InitializeAsync();
        var id = Guid.NewGuid().ToString();
        var serializer = Serializer();
        var value = new Changed(id, "replay");
        var entry = new ProjectionDeadLetter(ProjectionFailureProcessor.GetDeadLetterId("issue65", "partition", id),
            "issue65", "partition", id, 5, serializer.Serialize(value), "Failure", 3, DateTimeOffset.UtcNow);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => store.PutAsync(entry)));
        var restarted = new SqlServerProjectionDeadLetterStore(connection);
        var saved = await restarted.GetAsync(entry.Id);
        Assert.That(saved, Is.EqualTo(entry));
        Assert.That(serializer.Deserialize<Changed>(saved!.Event.TypeId, saved.Event.Data), Is.EqualTo(value));
        Assert.ThrowsAsync<InvalidOperationException>(async () => await restarted.PutAsync(entry with { Event = serializer.Serialize(value with { Name = "collision" }) }));
        var count = 0;
        await foreach (var item in restarted.ReadAsync()) if (item.Id == entry.Id) count++;
        Assert.That(count, Is.EqualTo(1));
        var options = new ProjectionErrorHandlingOptions { SerializeDeadLetterEvent = e => serializer.Serialize((Changed)e) };
        var disposition = await new ProjectionFailureProcessor().ExecuteAsync(_ => throw new AssertionException("Crash recovery reran handler"),
            "issue65", "partition", 5, [new(id, value, typeof(Changed))], options, deadLetters: restarted);
        Assert.That(disposition, Is.EqualTo(ProjectionDisposition.Quarantined));
    }
    [Test]
    public async Task RabbitMq_RoundTripUsesSameIdentityAsDatabaseProviders()
    {
        var uri = Require("TEST_RABBITMQ_CONNECTION_STRING");
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.UseRabbitMqEventBus(options =>
        { options.ConnectionUri = uri; options.ExchangeName = "issue64-" + Guid.NewGuid(); options.EventSerializer = Serializer(); });
        await using var services = collection.BuildServiceProvider();
        var bus = services.GetRequiredService<IEventBus>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var reader = bus.Subscribe<Changed>(timeout.Token).GetAsyncEnumerator(timeout.Token);
        var receive = reader.MoveNextAsync().AsTask();
        var value = new Changed("1", "broker");
        // The subscription declares its queue asynchronously; repeat publishing until it is bound.
        while (!receive.IsCompleted)
        {
            await bus.Publish(value, timeout.Token);
            await Task.WhenAny(receive, Task.Delay(100, timeout.Token));
            timeout.Token.ThrowIfCancellationRequested();
        }
        Assert.That(await receive, Is.True);
        Assert.That(reader.Current, Is.EqualTo(value));
    }
}
