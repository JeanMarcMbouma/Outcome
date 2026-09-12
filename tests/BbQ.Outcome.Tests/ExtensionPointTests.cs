using NUnit.Framework;

namespace BbQ.Outcome.Tests;

[TestFixture]
public sealed class ExtensionPointTests
{
    [Test]
    public void FailureFactoriesRejectNullEmptyAndNullElements()
    {
        Assert.Throws<ArgumentNullException>(() => Outcome<int, string>.FromErrors((IReadOnlyList<string>)null!));
        Assert.Throws<ArgumentException>(() => Outcome<int, string>.FromErrors(Array.Empty<string>()));
        Assert.Throws<ArgumentException>(() => Outcome<int, string>.FromErrors(new string[] { null! }));
        Assert.Throws<ArgumentNullException>(() => Outcome<int, string>.FromError(null!));
        Assert.Throws<ArgumentNullException>(() => Outcome<int>.FromErrors(null!));
        Assert.Throws<ArgumentException>(() => Outcome<int>.FromErrors(Array.Empty<object?>()));
        Assert.Throws<ArgumentException>(() => Outcome<int>.FromErrors(new object?[] { null }));
    }

    [Test]
    public void NullSuccessIsStillSupported()
    {
        Assert.That(Outcome<string?, string>.From(null).Value, Is.Null);
        Assert.That(Outcome<string?>.From(null).Value, Is.Null);
    }

    [Test]
    public void DefaultCannotBeConsumedOrCombinedIntoSuccess()
    {
        var typed = default(Outcome<int, string>);
        var untyped = default(Outcome<int>);
        Assert.That(typed.IsError, Is.True);
        Assert.Throws<InvalidOperationException>(() => { _ = typed.Errors; });
        Assert.Throws<InvalidOperationException>(() => { _ = untyped.Errors; });
        Assert.Throws<InvalidOperationException>(() => typed.Deconstruct(out var _, out var _, out var _));
        Assert.Throws<InvalidOperationException>(() => untyped.Deconstruct(out var _, out var _));
        Assert.Throws<InvalidOperationException>(() => typed.Map(x => x + 1));
        Assert.Throws<InvalidOperationException>(() => untyped.Match(x => x, _ => 0));
        Assert.Throws<InvalidOperationException>(() => Outcome<int, string>.Combine(typed));
        Assert.Throws<InvalidOperationException>(() => Outcome<int>.Combine(untyped));
        Assert.ThrowsAsync<InvalidOperationException>(async () => await Outcome<int, string>.CombineAsync(Task.FromResult(typed)));
        Assert.ThrowsAsync<InvalidOperationException>(async () => await Outcome<int>.CombineAsync(Task.FromResult(untyped)));
    }

    [Test]
    public void ErrorCollectionIsASnapshotAndCanBeSharedAcrossValueTypes()
    {
        var input = new List<string> { "first", "second" };
        var outcome = Outcome<int, string>.FromErrors(input);
        input[0] = "changed";
        input.Clear();
        Assert.That(outcome.Errors, Is.EqualTo(new[] { "first", "second" }));
        Assert.That(outcome.Errors, Is.Not.InstanceOf<IList<string>>());
        var propagated = outcome.Map(x => x.ToString());
        Assert.That(propagated.Errors, Is.SameAs(outcome.Errors));
    }

    [Test]
    public void HeterogeneousErrorsAreAlsoSnapshotted()
    {
        object?[] errors = ["original", 42];
        var outcome = Outcome<int>.FromErrors(errors);
        errors[0] = "changed";
        Assert.That(outcome.Errors, Is.EqualTo(new object?[] { "original", 42 }));
        Assert.That(outcome.Map(x => x.ToString()).Errors, Is.SameAs(outcome.Errors));
    }

    [Test]
    public async Task CombinePreservesFailureOrderAndEmptyInputSuccess()
    {
        var a = Outcome<int, string>.FromErrors(new[] { "a", "b" });
        var b = Outcome<int, string>.FromError("c");
        Assert.That(Outcome<int, string>.Combine(a, Outcome<int, string>.From(1), b).Errors,
            Is.EqualTo(new[] { "a", "b", "c" }));
        var result = await Outcome<int, string>.CombineAsync(Task.FromResult(a), Task.FromResult(b));
        Assert.That(result.Errors, Is.EqualTo(new[] { "a", "b", "c" }));
        Assert.That(Outcome<int, string>.Combine().Value, Is.Empty);
        Assert.That((await Outcome<int>.CombineAsync()).Value, Is.Empty);
    }

    [Test]
    public void MapErrorChangesErrorTypeInOrderAndSkipsSuccess()
    {
        var calls = new List<string>();
        var failure = Outcome<int, string>.FromErrors(new[] { "one", "three" });
        var mapped = failure.MapError(error => { calls.Add(error); return error.Length; });
        Assert.That(mapped.Errors, Is.EqualTo(new[] { 3, 5 }));
        Assert.That(calls, Is.EqualTo(new[] { "one", "three" }));
        var success = Outcome<int, string>.From(12).MapError(error => { Assert.Fail(); return error.Length; });
        Assert.That(success.Value, Is.EqualTo(12));
    }

    [Test]
    public void MapErrorsRunsOnceAndRejectsEmptyOrNullMappings()
    {
        var calls = 0;
        var outcome = Outcome<int, string>.FromErrors(new[] { "a", "b" });
        var mapped = outcome.MapErrors<int, string, int>(errors => { calls++; return new[] { errors.Count }; });
        Assert.That(mapped.Errors, Is.EqualTo(new[] { 2 }));
        Assert.That(calls, Is.EqualTo(1));
        Assert.Throws<ArgumentException>(() => outcome.MapErrors<int, string, string>(_ => Array.Empty<string>()));
        Assert.Throws<ArgumentNullException>(() => outcome.MapErrors<int, string, string>(_ => null!));
        Assert.Throws<ArgumentException>(() => outcome.MapError(_ => (string)null!));
    }

    [Test]
    public void RecoverOnlyRunsOnFailureAndCanReturnAnotherFailure()
    {
        var calls = 0;
        var recovered = Outcome<int, string>.FromError("missing").Recover(errors =>
        {
            calls++;
            Assert.That(errors[0], Is.EqualTo("missing"));
            return Outcome<int, string>.From(42);
        });
        Assert.That(recovered.Value, Is.EqualTo(42));
        var success = recovered.Recover(_ => { calls++; return Outcome<int, string>.From(0); });
        Assert.That(success.Value, Is.EqualTo(42));
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(Outcome<int, string>.FromError("a").Recover(_ => Outcome<int, string>.FromError("b")).Errors[0], Is.EqualTo("b"));
    }

    [Test]
    public void ObserversAreBranchSpecificAndPreserveOriginalErrors()
    {
        var successCalls = 0;
        var errorCalls = 0;
        var failure = Outcome<int, string>.FromError("error");
        var observed = failure.Tap(_ => successCalls++).TapError(_ => errorCalls++);
        Assert.That(observed.Errors, Is.SameAs(failure.Errors));
        var success = Outcome<int, string>.From(7).Tap(_ => successCalls++).TapError(_ => errorCalls++);
        Assert.That(success.Value, Is.EqualTo(7));
        Assert.That(successCalls, Is.EqualTo(1));
        Assert.That(errorCalls, Is.EqualTo(1));
    }

    [Test]
    public void HeterogeneousOperatorsCanEnterTypedPipelines()
    {
        var failure = Outcome<int>.FromErrors(new object?[] { "no", 123 });
        var mapped = failure.MapError(error => error!.ToString()!);
        Assert.That(mapped.Errors, Is.EqualTo(new[] { "no", "123" }));
        Assert.That(failure.MapErrors<int, string>(errors => new[] { $"count:{errors.Count}" }).Errors[0], Is.EqualTo("count:2"));
        var calls = 0;
        var observed = failure.Tap(_ => Assert.Fail()).TapError(_ => calls++);
        Assert.That(observed.Errors, Is.SameAs(failure.Errors));
        var recovered = observed.Recover(_ => Outcome<int>.From(42)).Tap(value => Assert.That(value, Is.EqualTo(42)));
        Assert.That(recovered.Value, Is.EqualTo(42));
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void CallbackExceptionsAreNotSwallowed()
    {
        var exception = new ApplicationException("observer");
        Assert.That(Assert.Throws<ApplicationException>(() => Outcome<int, string>.From(1).Tap(_ => throw exception)), Is.SameAs(exception));
        Assert.That(Assert.Throws<ApplicationException>(() => Outcome<int>.FromErrors(new object?[] { "e" }).TapError(_ => throw exception)), Is.SameAs(exception));
        Assert.Throws<ApplicationException>(() => Outcome<int, string>.FromError("e").MapError<int, string, int>(_ => throw exception));
        Assert.Throws<ApplicationException>(() => Outcome<int, string>.FromError("e").Recover(_ => throw exception));
    }

    [Test]
    public void NullDelegatesAreRejectedEvenOnTheInactiveBranch()
    {
        var success = Outcome<int, string>.From(1);
        Assert.Throws<ArgumentNullException>(() => success.MapError<int, string, int>(null!));
        Assert.Throws<ArgumentNullException>(() => success.MapErrors<int, string, int>(null!));
        Assert.Throws<ArgumentNullException>(() => success.Recover(null!));
        Assert.Throws<ArgumentNullException>(() => success.TapError(null!));
        Assert.Throws<ArgumentNullException>(() => Outcome<int>.FromErrors(new object?[] { "e" }).Tap(null!));
    }
}
