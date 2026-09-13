using System.Runtime.CompilerServices;
using BbQ.Events.Engine;
using BbQ.Events.Serialization;
using NUnit.Framework;

namespace BbQ.Cqrs.Tests;

[TestFixture]
public class ProjectionFailurePolicyTests
{
    private static ProjectionErrorHandlingOptions Options() => new()
    {
        InitialRetryDelayMs = 1, MaxRetryDelayMs = 2, MaxRetryAttempts = 3,
        SerializeDeadLetterEvent = value => new LegacyJsonEventSerializer().Serialize((string)value)
    };
    private static ProjectionFailureEvent[] Events(params string[] ids) => ids.Select(id => new ProjectionFailureEvent(id, id, typeof(string))).ToArray();
    private static Task Fail(CancellationToken _) => Task.FromException(new InvalidOperationException("password=secret"));

    [Test]
    public async Task Default_RetriesExactlyConfiguredTotalAttemptsThenSkips()
    {
        var calls = 0;
        var result = await new ProjectionFailureProcessor().ExecuteAsync(_ => { calls++; return Fail(_); }, "p", "a", 0, Events("1"), Options());
        Assert.That(calls, Is.EqualTo(3));
        Assert.That(result, Is.EqualTo(ProjectionDisposition.Skipped));
    }

    [Test]
    public async Task TransientPolicy_RetriesThenProjects()
    {
        var calls = 0;
        var policy = new Policy(context => context.Exception is TimeoutException ? ProjectionErrorHandlingStrategy.Retry : ProjectionErrorHandlingStrategy.Stop);
        var result = await new ProjectionFailureProcessor().ExecuteAsync(_ => ++calls == 1 ? Task.FromException(new TimeoutException()) : Task.CompletedTask,
            "p", "a", 8, Events("1"), Options(), policy);
        Assert.That(result, Is.EqualTo(ProjectionDisposition.Projected));
        Assert.That(calls, Is.EqualTo(2));
        Assert.That(policy.Context!.Position, Is.EqualTo(8));
        Assert.That(policy.Context.PartitionKey, Is.EqualTo("a"));
        Assert.That(policy.Context.Attempt, Is.EqualTo(1));
    }

    [TestCase(ProjectionErrorHandlingStrategy.Skip, ProjectionDisposition.Skipped)]
    [TestCase(ProjectionErrorHandlingStrategy.Stop, ProjectionDisposition.Stopped)]
    public async Task PermanentPolicy_DoesNotRetry(ProjectionErrorHandlingStrategy strategy, ProjectionDisposition expected)
    {
        var policy = new Policy(_ => strategy);
        var result = await new ProjectionFailureProcessor().ExecuteAsync(Fail, "p", "a", 0, Events("1"), Options(), policy);
        Assert.That(result, Is.EqualTo(expected));
        Assert.That(policy.Context!.Attempt, Is.EqualTo(1));
    }

    [Test]
    public void PolicyException_PropagatesWithoutDisposition() => Assert.ThrowsAsync<ApplicationException>(async () =>
        await new ProjectionFailureProcessor().ExecuteAsync(Fail, "p", "a", 0, Events("1"), Options(), new Policy(_ => throw new ApplicationException())));

    [Test]
    public void Cancellation_IsNeverClassified()
    {
        var policy = new Policy(_ => throw new AssertionException("Cancellation reached policy"));
        Assert.ThrowsAsync<OperationCanceledException>(async () => await new ProjectionFailureProcessor().ExecuteAsync(
            _ => Task.FromException(new OperationCanceledException()), "p", "a", 0, Events("1"), Options(), policy));
    }

    [Test]
    public async Task Quarantine_IsDurableBeforeReturnAndRecoveryAvoidsReprocessing()
    {
        var store = new MemoryStore();
        var processor = new ProjectionFailureProcessor();
        var policy = new Policy(_ => ProjectionErrorHandlingStrategy.Quarantine);
        var result = await processor.ExecuteAsync(Fail, "p", "a", 0, Events("1"), Options(), policy, store);
        Assert.That(result, Is.EqualTo(ProjectionDisposition.Quarantined));
        Assert.That(store.Items.Count, Is.EqualTo(1));
        Assert.That(store.Items.Values.Single().ErrorCode, Does.Not.Contain("secret"));
        result = await new ProjectionFailureProcessor().ExecuteAsync(_ => throw new AssertionException("Recovered quarantine must not run handler"),
            "p", "a", 0, Events("1"), Options(), policy, store);
        Assert.That(result, Is.EqualTo(ProjectionDisposition.Quarantined));
    }

    [Test]
    public void FailedPersistence_DoesNotReturnQuarantined()
    {
        var store = new MemoryStore { FailWrite = 1 };
        Assert.ThrowsAsync<IOException>(async () => await new ProjectionFailureProcessor().ExecuteAsync(Fail, "p", "a", 0,
            Events("1"), Options(), new Policy(_ => ProjectionErrorHandlingStrategy.Quarantine), store));
        Assert.That(store.Items, Is.Empty);
    }

    [Test]
    public async Task PartialBatchPersistence_RestartRetriesWholeBatchAndDeduplicates()
    {
        var store = new MemoryStore { FailWrite = 2 };
        var policy = new Policy(_ => ProjectionErrorHandlingStrategy.Quarantine);
        Assert.ThrowsAsync<IOException>(async () => await new ProjectionFailureProcessor().ExecuteAsync(Fail, "p", "a", 0, Events("1", "2"), Options(), policy, store));
        Assert.That(store.Items.Count, Is.EqualTo(1));
        store.FailWrite = 0;
        var result = await new ProjectionFailureProcessor().ExecuteAsync(Fail, "p", "a", 0, Events("1", "2"), Options(), policy, store);
        Assert.That(result, Is.EqualTo(ProjectionDisposition.Quarantined));
        Assert.That(store.Items.Count, Is.EqualTo(2));
    }

    [Test]
    public void MissingStableId_PreventsQuarantine() => Assert.ThrowsAsync<InvalidOperationException>(async () =>
        await new ProjectionFailureProcessor().ExecuteAsync(Fail, "p", "a", 0, Events(""), Options(), new Policy(_ => ProjectionErrorHandlingStrategy.Quarantine), new MemoryStore()));

    [Test]
    public void DispositionCounters_DoNotCountFailuresAsSuccess()
    {
        var monitor = new InMemoryProjectionMonitor();
        monitor.RecordDisposition("p", "a", 1, ProjectionDisposition.Skipped);
        monitor.RecordDisposition("p", "a", 2, ProjectionDisposition.Quarantined);
        var metrics = monitor.GetMetrics("p", "a")!;
        Assert.That(metrics.EventsProcessed, Is.Zero);
        Assert.That(metrics.EventsSkipped, Is.EqualTo(1));
        Assert.That(metrics.EventsQuarantined, Is.EqualTo(1));
        Assert.That(metrics.CurrentPosition, Is.EqualTo(2));
    }

    private sealed class Policy(Func<ProjectionFailureContext, ProjectionErrorHandlingStrategy> choose) : IProjectionFailurePolicy
    {
        public ProjectionFailureContext? Context;
        public ValueTask<ProjectionErrorHandlingStrategy> DecideAsync(ProjectionFailureContext context, CancellationToken ct = default)
        { Context = context; return ValueTask.FromResult(choose(context)); }
    }
    private sealed class MemoryStore : IProjectionDeadLetterStore
    {
        public Dictionary<string, ProjectionDeadLetter> Items = new();
        public int FailWrite, Writes;
        public Task PutAsync(ProjectionDeadLetter entry, CancellationToken ct = default)
        {
            if (++Writes == FailWrite) throw new IOException("Store unavailable");
            Items.TryAdd(entry.Id, entry);
            return Task.CompletedTask;
        }
        public Task<ProjectionDeadLetter?> GetAsync(string id, CancellationToken ct = default) => Task.FromResult(Items.GetValueOrDefault(id));
        public async IAsyncEnumerable<ProjectionDeadLetter> ReadAsync([EnumeratorCancellation] CancellationToken ct = default)
        { foreach (var item in Items.Values) { yield return item; await Task.Yield(); } }
    }
}
