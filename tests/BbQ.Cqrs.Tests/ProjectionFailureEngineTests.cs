using System.Runtime.CompilerServices;
using BbQ.Events.Checkpointing;
using BbQ.Events.Configuration;
using BbQ.Events.Engine;
using BbQ.Events.Events;
using BbQ.Events.Projections;
using BbQ.Events.Serialization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BbQ.Cqrs.Tests;

[TestFixture, NonParallelizable]
public class ProjectionFailureEngineTests
{
    public record Item(string Id);
    public sealed class Single : IProjectionHandler<Item>
    {
        public ValueTask ProjectAsync(Item value, CancellationToken ct = default) => value.Id == "1"
            ? ValueTask.FromException(new InvalidOperationException("secret")) : ValueTask.CompletedTask;
    }
    public sealed class Batch : IProjectionBatchHandler<Item>
    {
        public ValueTask ProjectBatchAsync(IReadOnlyList<Item> values, CancellationToken ct = default) => ValueTask.FromException(new InvalidOperationException("secret"));
    }
    private sealed class FiniteBus : IEventBus
    {
        public Task Publish<T>(T value, CancellationToken ct = default) => Task.CompletedTask;
        public async IAsyncEnumerable<T> Subscribe<T>([EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var id in new[] { "1", "2", "3" }) { yield return (T)(object)new Item(id); await Task.Yield(); }
        }
    }
    private sealed class BlockingStore : IProjectionDeadLetterStore
    {
        public TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Fail;
        public List<ProjectionDeadLetter> Items = new();
        public async Task PutAsync(ProjectionDeadLetter entry, CancellationToken ct = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(ct);
            if (Fail) throw new IOException("Disk failed");
            Items.Add(entry);
        }
        public Task<ProjectionDeadLetter?> GetAsync(string id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(item => item.Id == id));
        public async IAsyncEnumerable<ProjectionDeadLetter> ReadAsync([EnumeratorCancellation] CancellationToken ct = default)
        { foreach (var item in Items) { yield return item; await Task.Yield(); } }
    }
    private static ServiceProvider Provider(bool batch, ProjectionErrorHandlingStrategy strategy, BlockingStore? letters = null, IProjectionCheckpointStore? checkpoints = null)
    {
        ProjectionHandlerRegistry.Clear();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IEventBus, FiniteBus>();
        services.AddSingleton<IProjectionMonitor, InMemoryProjectionMonitor>();
        void Configure(ProjectionOptions options)
        {
            options.ProjectionName = "failure-test";
            options.CheckpointBatchSize = 1;
            options.BatchSize = batch ? 3 : 0;
            options.ErrorHandling.Strategy = strategy;
            options.ErrorHandling.DeadLetterStore = letters;
            options.ErrorHandling.EventIdSelector = value => ((Item)value).Id;
            options.ErrorHandling.SerializeDeadLetterEvent = value => new LegacyJsonEventSerializer().Serialize((Item)value);
        }
        if (batch) services.AddProjection<Batch>(Configure);
        else services.AddProjection<Single>(Configure);
        if (checkpoints != null) services.AddSingleton(checkpoints);
        services.AddProjectionEngine();
        return services.BuildServiceProvider();
    }
    [TearDown] public void Cleanup() => ProjectionHandlerRegistry.Clear();

    private sealed class FailingCheckpoint : IProjectionCheckpointStore
    {
        public ValueTask<long?> GetCheckpointAsync(string name, CancellationToken ct = default) => ValueTask.FromResult<long?>(null);
        public ValueTask SaveCheckpointAsync(string name, long position, CancellationToken ct = default) => ValueTask.FromException(new IOException("Checkpoint unavailable"));
        public ValueTask ResetCheckpointAsync(string name, CancellationToken ct = default) => ValueTask.CompletedTask;
    }
    [Test]
    public async Task CheckpointFailure_AfterPersistence_RestartRecoversQuarantine()
    {
        var letters = new BlockingStore();
        letters.Release.TrySetResult();
        using (var first = Provider(false, ProjectionErrorHandlingStrategy.Quarantine, letters, new FailingCheckpoint()))
            Assert.ThrowsAsync<IOException>(async () => await first.GetRequiredService<IProjectionEngine>().RunAsync().WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.That(letters.Items.Count, Is.EqualTo(1));
        using var restarted = Provider(false, ProjectionErrorHandlingStrategy.Quarantine, letters);
        await restarted.GetRequiredService<IProjectionEngine>().RunAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(letters.Items.Count, Is.EqualTo(1), "Recovery must not persist another copy");
        Assert.That(await restarted.GetRequiredService<IProjectionCheckpointStore>().GetCheckpointAsync("failure-test:_default"), Is.EqualTo(3));
    }
    [TestCase(false)]
    [TestCase(true)]
    public async Task Skip_AdvancesCheckpointWithDistinctCounters(bool batch)
    {
        using var provider = Provider(batch, ProjectionErrorHandlingStrategy.Skip);
        await provider.GetRequiredService<IProjectionEngine>().RunAsync().WaitAsync(TimeSpan.FromSeconds(10));
        var checkpoint = await provider.GetRequiredService<IProjectionCheckpointStore>().GetCheckpointAsync("failure-test:_default");
        var metrics = provider.GetRequiredService<IProjectionMonitor>().GetMetrics("failure-test", "_default")!;
        Assert.That(checkpoint, Is.EqualTo(3));
        Assert.That(metrics.EventsProcessed, Is.EqualTo(batch ? 0 : 2));
        Assert.That(metrics.EventsSkipped, Is.EqualTo(batch ? 3 : 1));
    }
    [TestCase(false)]
    [TestCase(true)]
    public async Task Stop_DoesNotAdvancePastFailure(bool batch)
    {
        using var provider = Provider(batch, ProjectionErrorHandlingStrategy.Stop);
        await provider.GetRequiredService<IProjectionEngine>().RunAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(await provider.GetRequiredService<IProjectionCheckpointStore>().GetCheckpointAsync("failure-test:_default"), Is.Null);
    }
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public async Task Quarantine_CheckpointWaitsForDurableWrite(bool batch, bool fail)
    {
        var store = new BlockingStore { Fail = fail };
        using var provider = Provider(batch, ProjectionErrorHandlingStrategy.Quarantine, store);
        var run = provider.GetRequiredService<IProjectionEngine>().RunAsync();
        await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var checkpoints = provider.GetRequiredService<IProjectionCheckpointStore>();
        Assert.That(await checkpoints.GetCheckpointAsync("failure-test:_default"), Is.Null);
        store.Release.TrySetResult();
        if (fail)
        {
            Assert.ThrowsAsync<IOException>(async () => await run.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.That(await checkpoints.GetCheckpointAsync("failure-test:_default"), Is.Null);
        }
        else
        {
            await run.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.That(await checkpoints.GetCheckpointAsync("failure-test:_default"), Is.EqualTo(3));
            Assert.That(store.Items.Count, Is.EqualTo(batch ? 3 : 1));
        }
    }
}
