using BbQ.Events.RabbitMQ.Configuration;
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
    private static JsonEventSerializer Serializer() => new(new EventTypeRegistry().Register<Changed>("sample.changed", 1, typeof(Changed).FullName!));
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
        Assert.That(events.Select(e => e.Event.Name), Is.EqualTo(new[] { "historical", "current" }));
        Assert.That(events[1].Position, Is.GreaterThan(events[0].Position));
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
