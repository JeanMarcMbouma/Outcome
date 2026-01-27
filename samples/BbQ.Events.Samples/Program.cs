using BbQ.Events.Configuration;
using BbQ.Events.Engine;
using BbQ.Events.Events;
using BbQ.Events.Schema;
using BbQ.Events.SqlServer.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Runtime.CompilerServices;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        // Additional configuration can be set up here if needed
    })
    .ConfigureServices((context, services) =>
    {
        services.AddLogging();

        services.AddInMemoryEventBus()
            .AddProjectionEngine()
            .AddProjection<UserProjection>(options =>
            {
                options.StartupMode = ProjectionStartupMode.Replay;
            });
        services.UseSqlServerEventStore(options =>
        {
            options.ConnectionString = "server=.;database=event_sourcing;Integrated Security=true;TrustServerCertificate=true";
            options.AutoCreateSchema = true;
            options.IncludeMetadata = true;
        });

        services.UseSqlServerCheckpoints("server=.;database=event_sourcing;Integrated Security=true;TrustServerCertificate=true");

        services.AddScoped<IEventHandler<UserCreatedEvent>, UserCreatedEventHandler>();
        services.AddScoped<UserCreatedSubscriber>();
    });

var app = builder.Build();
var eventBus = app.Services.GetRequiredService<IEventBus>();
var eventStore = app.Services.GetRequiredService<IEventStore>();
var initializer = app.Services.GetRequiredService<ISchemaInitializer>();
var engine = app.Services.GetRequiredService<IProjectionEngine>();
var rebuilder = app.Services.GetRequiredService<IProjectionRebuilder>();
var replayService = app.Services.GetRequiredService<IReplayService>();



var userCreatedEvent = new UserCreatedEvent("1", $"JohnDoe @ {DateTime.UtcNow}");
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));

_ = engine.RunAsync(cts.Token);

await initializer.EnsureSchemaAsync(cts.Token);

//await rebuilder.ResetAllProjectionsAsync(cts.Token);

//await eventStore.AppendAsync(nameof(UserProjection), userCreatedEvent);


//await eventBus.Publish(userCreatedEvent, cts.Token);
//var sub = serviceProvider.GetRequiredService<UserCreatedSubscriber>();
//var projection = serviceProvider.GetRequiredService<UserProjection>();

//await foreach (var item in sub.Subscribe(cts.Token))
//{
//    await projection.ProjectAsync(item, cts.Token);
//}

await replayService.ReplayAsync(nameof(UserProjection), new ReplayOptions
{
    BatchSize = 10,
    FromCheckpoint = false,
    FromPosition = 0
}, cts.Token);

Console.ReadKey();

record UserCreatedEvent(string UserId, string UserName);

class UserCreatedEventHandler : IEventHandler<UserCreatedEvent>
{
    public Task Handle(UserCreatedEvent @event, CancellationToken ct = default)
    {
        Console.WriteLine($"User created: {@event.UserId}, {@event.UserName}");

        return Task.CompletedTask;
    }
}

class UserCreatedSubscriber(IEventStore store) : IEventSubscriber<UserCreatedEvent>
{
    public async IAsyncEnumerable<UserCreatedEvent> Subscribe([EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in store.ReadAsync<UserCreatedEvent>("users", 0, ct))
        {
            yield return item.Event;
        }
    }
}

class UserProjection : BbQ.Events.Projections.IProjectionHandler<UserCreatedEvent>
{
    public ValueTask ProjectAsync(UserCreatedEvent @event, CancellationToken ct = default)
    {
        Console.WriteLine($"Projecting user created event: {@event.UserId}, {@event.UserName}");
        return ValueTask.CompletedTask;
    }
}
