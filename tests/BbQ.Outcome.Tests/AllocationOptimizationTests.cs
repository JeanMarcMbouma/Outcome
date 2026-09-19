using NUnit.Framework;

namespace BbQ.Outcome.Tests;

[TestFixture]
public class AllocationOptimizationTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void ArraySnapshotsRejectNullAtEveryPosition(int index)
    {
        var errors = new[] { "a", "b", "c" };
        errors[index] = null!;
        Assert.Throws<ArgumentException>(() => Outcome<int, string>.FromErrors(errors));
    }

    [Test]
    public void SingleErrorSnapshotRemainsImmutableAndEnumerable()
    {
        var input = new[] { "original" };
        var outcome = Outcome<int, string>.FromErrors(input);
        input[0] = "changed";
        Assert.That(outcome.Errors, Is.EqualTo(new[] { "original" }));
        Assert.That(outcome.Errors, Is.Not.InstanceOf<IList<string>>());
        Assert.Throws<IndexOutOfRangeException>(() => _ = outcome.Errors[-1]);
        Assert.Throws<IndexOutOfRangeException>(() => _ = outcome.Errors[1]);
        Assert.That(outcome.Map(static value => value.ToString()).Errors, Is.SameAs(outcome.Errors));
    }

    [Test]
    public void CompositionReusesTheOnlyFailureAndDoesNotHideInvalidInputs()
    {
        var failure = Outcome<int, string>.FromErrors(new[] { "one", "two" });
        var inputs = new[] { Outcome<int, string>.From(1), failure, Outcome<int, string>.From(3) };
        Assert.That(inputs.Sequence().Errors, Is.SameAs(failure.Errors));
        Assert.That(Outcome<int, string>.Combine(inputs).Errors, Is.SameAs(failure.Errors));
        Assert.Throws<InvalidOperationException>(() => new[] { failure, default(Outcome<int, string>) }.Sequence());
        Assert.Throws<InvalidOperationException>(() => Outcome<int, string>.Combine(failure, default));
    }

    [Test]
    public void MergedErrorsAndSuccessfulValuesAreIndependentOfInputArrays()
    {
        var first = Outcome<int, string>.FromErrors(new[] { "a", "b" });
        var second = Outcome<int, string>.FromErrors(new[] { "c", "d" });
        var inputs = new[] { first, Outcome<int, string>.From(2), second };
        var combined = Outcome<int, string>.Combine(inputs);
        var sequenced = inputs.Sequence();
        var zipped = first.Zip(second);
        Array.Fill(inputs, Outcome<int, string>.From(0));
        Assert.That(combined.Errors, Is.EqualTo(new[] { "a", "b", "c", "d" }));
        Assert.That(sequenced.Errors, Is.EqualTo(combined.Errors));
        Assert.That(zipped.Errors, Is.EqualTo(combined.Errors));
        Assert.That(combined.Errors, Is.Not.InstanceOf<IList<string>>());
        var success = inputs.Sequence();
        var combinedSuccess = Outcome<int, string>.Combine(inputs);
        inputs[0] = Outcome<int, string>.From(9);
        Assert.That(success.Value, Is.EqualTo(new[] { 0, 0, 0 }));
        Assert.That(combinedSuccess.Value, Is.EqualTo(new[] { 0, 0, 0 }));
        Assert.That(((IList<int>)success.Value).IsReadOnly, Is.True);
    }

    [Test]
    public async Task TraversalKeepsVisitingInputsAndOrdersErrorsAcrossBatches()
    {
        var visited = new List<int>();
        var result = await Enumerable.Range(0, 7).TraverseAsync<int, int, string>((value, _) =>
        {
            visited.Add(value);
            return Task.FromResult(value % 2 == 0
                ? Outcome<int, string>.FromErrors(new[] { $"{value}a", $"{value}b" })
                : Outcome<int, string>.From(value));
        }, maxConcurrency: 3);
        Assert.That(visited, Is.EqualTo(Enumerable.Range(0, 7)));
        Assert.That(result.Errors, Is.EqualTo(new[] { "0a", "0b", "2a", "2b", "4a", "4b", "6a", "6b" }));
    }
}
