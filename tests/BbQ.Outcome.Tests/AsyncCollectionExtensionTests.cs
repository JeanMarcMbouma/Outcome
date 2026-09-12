using NUnit.Framework;
using System.Runtime.CompilerServices;

namespace BbQ.Outcome.Tests;

[TestFixture]
public sealed class AsyncCollectionExtensionTests
{
    [Test]
    public async Task MatchAwaitsOnlyTheChosenAsyncBranch()
    {
        var calls = 0;
        var value = await Task.FromResult(Outcome<int, string>.From(7)).MatchAsync(
            async (x, _) => { await Task.Yield(); calls++; return x + 1; },
            (_, _) => throw new AssertionException("Failure branch must not run"));
        Assert.That(value, Is.EqualTo(8));
        Assert.That(calls, Is.EqualTo(1));
        var error = await Task.FromResult(Outcome<int>.FromErrors(new object?[] { "missing" })).MatchAsync(
            (_, _) => throw new AssertionException("Success branch must not run"),
            async (errors, _) => { await Task.Yield(); return errors[0]!.ToString(); });
        Assert.That(error, Is.EqualTo("missing"));
    }

    [Test]
    public async Task ScalarCallbacksReceiveCancellationAndFailuresSkipCallbacks()
    {
        using var cancellation = new CancellationTokenSource();
        var result = await Outcome<int, string>.From(2).MapAsync((x, ct) =>
        {
            Assert.That(ct, Is.EqualTo(cancellation.Token));
            return Task.FromResult(x * 2);
        }, cancellation.Token).BindAsync((x, _) => Task.FromResult(Outcome<string, string>.From(x.ToString())));
        Assert.That(result.Value, Is.EqualTo("4"));
        var failure = Outcome<int, string>.FromError("bad");
        var propagated = await failure.MapAsync<int, string, int>((_, _) => throw new AssertionException("Must skip"));
        Assert.That(propagated.Errors, Is.SameAs(failure.Errors));
        cancellation.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () => await failure.MapAsync((x, _) => Task.FromResult(x), cancellation.Token));
    }

    [Test]
    public void CancellingAnExistingTaskWaitDoesNotCompleteItsProducer()
    {
        var source = new TaskCompletionSource<Outcome<int, string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () => await source.Task.MapAsync((x, _) => Task.FromResult(x), cancellation.Token));
        Assert.That(source.Task.IsCompleted, Is.False);
        source.SetResult(Outcome<int, string>.From(1));
    }

    [Test]
    public async Task AsyncObservationAndRecoveryPreserveBranchSemantics()
    {
        var failure = Outcome<int, string>.FromError("e");
        var calls = 0;
        var observed = await failure.TapErrorAsync(async (_, _) => { await Task.Yield(); calls++; });
        Assert.That(observed.Errors, Is.SameAs(failure.Errors));
        var recovered = await observed.RecoverAsync((_, _) => Task.FromResult(Outcome<int, string>.From(42)));
        var tapped = await recovered.TapAsync(async (x, _) => { await Task.Yield(); Assert.That(x, Is.EqualTo(42)); calls++; });
        Assert.That(tapped.Value, Is.EqualTo(42));
        Assert.That(calls, Is.EqualTo(2));
        Assert.ThrowsAsync<ApplicationException>(async () => await recovered.TapAsync((_, _) => throw new ApplicationException("visible")));
    }

    [Test]
    public async Task AsyncStreamsAwaitCallbacksAndPreserveFailures()
    {
        var calls = 0;
        var values = new List<Outcome<int, string>>();
        await foreach (var item in Stream().MapAsync(async (x, _) => { await Task.Yield(); calls++; return x * 2; })
            .BindAsync((x, _) => Task.FromResult(Outcome<int, string>.From(x + 1))))
            values.Add(item);
        Assert.That(values[0].Value, Is.EqualTo(3));
        Assert.That(values[1].Errors[0], Is.EqualTo("bad"));
        Assert.That(values[2].Value, Is.EqualTo(7));
        Assert.That(calls, Is.EqualTo(2));
    }

    [Test]
    public void AsyncStreamHonorsEnumeratorCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in Stream().MapAsync((x, _) => Task.FromResult(x)).WithCancellation(cancellation.Token)) { }
        });
    }

    private static async IAsyncEnumerable<Outcome<int, string>> Stream([EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
        yield return Outcome<int, string>.From(1);
        yield return Outcome<int, string>.FromError("bad");
        yield return Outcome<int, string>.From(3);
    }

    [Test]
    public void ZipSupportsDifferentValuesAndAccumulatesBothErrors()
    {
        var success = Outcome<int, string>.From(1).Zip(Outcome<string, string>.From("a"));
        Assert.That(success.Value, Is.EqualTo((1, "a")));
        var failure = Outcome<int, string>.FromError("left").Zip(Outcome<bool, string>.FromError("right"));
        Assert.That(failure.Errors, Is.EqualTo(new[] { "left", "right" }));
        Assert.That(Outcome<int>.From(2).Zip(Outcome<string>.From("b")).Value, Is.EqualTo((2, "b")));
    }

    [Test]
    public void SequenceAndTraverseEnumerateOnceAndAccumulateInOrder()
    {
        var enumerations = 0;
        IEnumerable<int> Inputs()
        {
            enumerations++;
            yield return 0;
            yield return 1;
            yield return 2;
        }
        var result = Inputs().Traverse(x => x == 1 ? Outcome<int, string>.From(x) : Outcome<int, string>.FromError($"e{x}"));
        Assert.That(result.Errors, Is.EqualTo(new[] { "e0", "e2" }));
        Assert.That(enumerations, Is.EqualTo(1));
        Assert.That(Array.Empty<Outcome<int, string>>().Sequence().Value, Is.Empty);
        Assert.Throws<InvalidOperationException>(() => new[] { default(Outcome<int, string>) }.Sequence());
        Assert.That(new[] { Outcome<int>.From(1), Outcome<int>.From(2) }.Sequence().Value, Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public async Task TraverseAsyncBoundsConcurrencyAndPreservesOrder()
    {
        var active = 0;
        var peak = 0;
        var gate = new object();
        var result = await Enumerable.Range(0, 7).TraverseAsync<int, string, string>(async (x, ct) =>
        {
            lock (gate) { active++; peak = Math.Max(peak, active); }
            try
            {
                await Task.Delay((7 - x) * 3, ct);
                return Outcome<string, string>.From(x.ToString());
            }
            finally { lock (gate) { active--; } }
        }, maxConcurrency: 3);
        Assert.That(result.Value, Is.EqualTo(Enumerable.Range(0, 7).Select(x => x.ToString())));
        Assert.That(peak, Is.InRange(1, 3));
        Assert.That(active, Is.Zero);
    }

    [Test]
    public async Task TraverseAsyncDoesNotFailFastOnDomainFailures()
    {
        var calls = 0;
        var result = await Enumerable.Range(0, 4).TraverseAsync<int, int, string>((x, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(x % 2 == 0 ? Outcome<int, string>.From(x) : Outcome<int, string>.FromError($"e{x}"));
        }, maxConcurrency: 2);
        Assert.That(result.Errors, Is.EqualTo(new[] { "e1", "e3" }));
        Assert.That(calls, Is.EqualTo(4));
    }

    [Test]
    public void TraverseAsyncDrainsStartedTasksAndPreservesSynchronousFaults()
    {
        var drained = false;
        async Task<Outcome<int, string>> Operation(int x, CancellationToken ct)
        {
            if (x == 1) throw new InvalidOperationException("original");
            try { await Task.Delay(Timeout.Infinite, ct); return Outcome<int, string>.From(x); }
            finally { drained = true; }
        }
        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new[] { 0, 1 }.TraverseAsync(Operation, maxConcurrency: 2));
        Assert.That(exception!.Message, Is.EqualTo("original"));
        Assert.That(drained, Is.True);
        Assert.ThrowsAsync<InvalidOperationException>(async () => await new[] { 1, 2 }.TraverseAsync<int, int, string>(
            (_, _) => throw new InvalidOperationException("first factory"), maxConcurrency: 2));
    }

    [Test]
    public void TraverseAsyncValidatesConcurrencyAndCancellationBeforeStarting()
    {
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await new[] { 1 }.TraverseAsync<int, int, string>(
            (x, _) => Task.FromResult(Outcome<int, string>.From(x)), maxConcurrency: 0));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () => await new[] { 1 }.TraverseAsync<int, int, string>(
            (_, _) => throw new AssertionException("Must not start"), cancellationToken: cancellation.Token));
    }

    [Test]
    public async Task HeterogeneousAsyncPipelineAndTraversalRemainHeterogeneous()
    {
        var result = await Task.FromResult(Outcome<int>.From(1)).MapAsync((x, _) => Task.FromResult(x + 1))
            .BindAsync((x, _) => Task.FromResult(Outcome<string>.From(x.ToString())));
        Assert.That(result.Value, Is.EqualTo("2"));
        var traversal = await new[] { 1, 2 }.TraverseAsync((x, _) => Task.FromResult(Outcome<int>.From(x * 2)), 2);
        Assert.That(traversal.Value, Is.EqualTo(new[] { 2, 4 }));
    }
}
