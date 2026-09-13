using BbQ.Outcome.Diagnostics;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.Diagnostics;

namespace BbQ.Outcome.Integrations.Tests;

[TestFixture]
public sealed class DiagnosticsTests
{
    private sealed class CaptureLogger : ILogger
    {
        public List<string> Messages { get; } = [];
        public List<Exception?> Exceptions { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            Exceptions.Add(exception);
        }
    }

    private static ActivityListener Listener(ActivitySource source, List<Activity> stopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = candidate => candidate.Name == source.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => stopped.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    [Test]
    public async Task ObservationPreservesOutcomesAndDoesNotExposeDescriptionsOrValues()
    {
        using var source = new ActivitySource("test-" + Guid.NewGuid());
        var stopped = new List<Activity>();
        using var listener = Listener(source, stopped);
        var logger = new CaptureLogger();
        var observer = new OutcomeObserver<WireError>(logger);
        var failure = Outcome<string, WireError>.FromError(new("PRIVATE_CODE", "PRIVATE_MESSAGE"));
        var observed = await observer.TraceAsync(source, "LoadUser", _ => Task.FromResult(failure));
        Assert.That(observed.Errors, Is.SameAs(failure.Errors));
        Assert.That(stopped.Single().Status, Is.EqualTo(ActivityStatusCode.Error));
        Assert.That(stopped[0].GetTagItem("outcome.error_count"), Is.EqualTo(1));
        Assert.That(stopped[0].GetTagItem("outcome.error_codes"), Is.Null);
        Assert.That(string.Join(" ", logger.Messages), Does.Not.Contain("PRIVATE"));
        var success = await observer.TraceAsync(source, "LoadUser", _ => Task.FromResult(Outcome<string, WireError>.From("PRIVATE_VALUE")));
        Assert.That(success.Value, Is.EqualTo("PRIVATE_VALUE"));
        Assert.That(stopped.Last().Status, Is.EqualTo(ActivityStatusCode.Ok));
        Assert.That(string.Join(" ", logger.Messages), Does.Not.Contain("PRIVATE"));
        Assert.That(logger.Exceptions, Is.All.Null);
    }

    [Test]
    public async Task OptInCodesAreBoundedAndDoNotIncludeMetadata()
    {
        using var source = new ActivitySource("test-" + Guid.NewGuid());
        var stopped = new List<Activity>();
        using var listener = Listener(source, stopped);
        var descriptors = new DelegateErrorDescriptorProvider<WireError>(error => new(error.Code, error.Message,
            Metadata: new Dictionary<string, object?> { ["secret"] = "PASSWORD" }));
        var observer = new OutcomeObserver<WireError>(descriptors: descriptors,
            options: new(IncludeErrorCodes: true, MaxErrorCodes: 1, MaxCodeLength: 4));
        await observer.TraceAsync(source, "Operation", _ => Task.FromResult(Outcome<int, WireError>.FromErrors(
            new[] { new WireError("LONG_CODE", "PRIVATE"), new WireError("OTHER", "PRIVATE") })));
        Assert.That(stopped.Single().GetTagItem("outcome.error_codes"), Is.EqualTo(new[] { "LONG" }));
        Assert.That(stopped.Single().TagObjects.Any(tag => tag.Key.Contains("secret")), Is.False);
        Assert.Throws<ArgumentException>(() => new OutcomeObserver<WireError>(options: new(IncludeErrorCodes: true)));
    }

    [Test]
    public void CancellationAndUnexpectedExceptionsRemainDistinct()
    {
        using var source = new ActivitySource("test-" + Guid.NewGuid());
        var stopped = new List<Activity>();
        using var listener = Listener(source, stopped);
        var logger = new CaptureLogger();
        var observer = new OutcomeObserver<string>(logger);
        Assert.CatchAsync<OperationCanceledException>(async () => await observer.TraceAsync<int>(source, "Cancelled",
            _ => Task.FromException<Outcome<int, string>>(new OperationCanceledException())));
        Assert.That(stopped[0].Status, Is.EqualTo(ActivityStatusCode.Unset));
        Assert.That(stopped[0].GetTagItem("outcome.cancelled"), Is.EqualTo(true));
        var exception = new InvalidOperationException("PRIVATE_MESSAGE");
        Assert.That(Assert.ThrowsAsync<InvalidOperationException>(async () => await observer.TraceAsync<int>(source, "Faulted",
            _ => Task.FromException<Outcome<int, string>>(exception))), Is.SameAs(exception));
        Assert.That(stopped[1].Status, Is.EqualTo(ActivityStatusCode.Error));
        Assert.That(string.Join(" ", logger.Messages), Does.Not.Contain("PRIVATE_MESSAGE"));
        Assert.That(logger.Exceptions, Is.All.Null);
    }

    [Test]
    public async Task NoListenerDoesNotModifyParentAndHeterogeneousObservationWorks()
    {
        using var parent = new Activity("parent").Start();
        using var source = new ActivitySource("unobserved-" + Guid.NewGuid());
        var observer = new OutcomeObserver<object?>();
        var failure = Outcome<int>.FromErrors(new object?[] { "private" });
        var result = await observer.TraceAsync(source, "Child", _ => Task.FromResult(failure));
        Assert.That(result.Errors, Is.SameAs(failure.Errors));
        Assert.That(parent.Status, Is.EqualTo(ActivityStatusCode.Unset));
        Assert.That(parent.GetTagItem("outcome.error_count"), Is.Null);
        var observed = result.Observe(observer, "ExplicitParentObservation");
        Assert.That(observed.Errors, Is.SameAs(failure.Errors));
        Assert.That(parent.Status, Is.EqualTo(ActivityStatusCode.Error));
    }
}
