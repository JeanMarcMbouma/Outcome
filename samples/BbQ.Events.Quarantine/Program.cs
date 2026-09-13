using BbQ.Events.Serialization;
using BbQ.Events.SqlServer.Events;
using BbQ.Events.SqlServer.Schema;

var connection = Environment.GetEnvironmentVariable("QUARANTINE_SQL_CONNECTION")
    ?? throw new InvalidOperationException("Set QUARANTINE_SQL_CONNECTION to the quarantine database.");
var letters = new SqlServerProjectionDeadLetterStore(connection);
var serializer = new JsonEventSerializer(new EventTypeRegistry().Register<OrderChanged>("orders.changed"));
if (args is ["list"])
{
    await foreach (var entry in letters.ReadAsync())
        Console.WriteLine($"{entry.Id} projection={entry.ProjectionName} partition={entry.PartitionKey} event={entry.EventId} type={entry.Event.TypeId} attempts={entry.Attempts} error={entry.ErrorCode}");
}
else if (args is ["replay", var id, var stream])
{
    if (!stream.StartsWith("replay-", StringComparison.Ordinal)) throw new ArgumentException("Use an isolated stream beginning replay-.");
    var entry = await letters.GetAsync(id) ?? throw new InvalidOperationException("Unknown dead letter.");
    var value = serializer.Deserialize<OrderChanged>(entry.Event.TypeId, entry.Event.Data);
    await new SqlServerSchemaInitializer(connection).EnsureSchemaAsync();
    var store = new SqlServerEventStore(new() { ConnectionString = connection, EventSerializer = serializer });
    var position = await store.AppendAsync(stream, value);
    Console.WriteLine($"Appended event {entry.EventId} to {stream} at {position}. Quarantine history retained.");
}
else Console.WriteLine("Usage: list | replay <dead-letter-id> <replay-stream>");

public record OrderChanged(string Id, decimal Total);
