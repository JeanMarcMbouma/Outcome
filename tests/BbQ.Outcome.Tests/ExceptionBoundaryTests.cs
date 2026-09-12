using NUnit.Framework;

namespace BbQ.Outcome.Tests;

[TestFixture]
public sealed class ExceptionBoundaryTests
{
    private static bool MapIo(Exception exception, out string error)
    {
        error = "IO_FAILURE";
        return exception is IOException;
    }

    [Test]
    public void TryMapsOnlyRecognizedExceptions()
    {
        Assert.That(Outcome.Try<int, string>(() => 42, MapIo).Value, Is.EqualTo(42));
        Assert.That(Outcome.Try<int, string>(() => throw new IOException("disk"), MapIo).Errors[0], Is.EqualTo("IO_FAILURE"));
        var unexpected = new InvalidOperationException("programming error");
        Assert.That(Assert.Throws<InvalidOperationException>(() => Outcome.Try<int, string>(() => throw unexpected, MapIo)), Is.SameAs(unexpected));
    }

    [Test]
    public async Task TryAsyncMapsRecognizedFailuresAndPassesToken()
    {
        using var cancellation = new CancellationTokenSource();
        var success = await Outcome.TryAsync<int, string>(ct =>
        {
            Assert.That(ct, Is.EqualTo(cancellation.Token));
            return Task.FromResult(42);
        }, MapIo, cancellation.Token);
        Assert.That(success.Value, Is.EqualTo(42));
        var failure = await Outcome.TryAsync<int, string>(_ => Task.FromException<int>(new IOException("disk")), MapIo);
        Assert.That(failure.Errors[0], Is.EqualTo("IO_FAILURE"));
    }

    [Test]
    public void CancellationNeverReachesTheExceptionMapper()
    {
        var calls = 0;
        bool Mapper(Exception _, out string error) { calls++; error = "should not happen"; return true; }
        Assert.Catch<OperationCanceledException>(() => Outcome.Try<int, string>(() => throw new OperationCanceledException(), Mapper));
        Assert.CatchAsync<OperationCanceledException>(async () => await Outcome.TryAsync<int, string>(
            _ => Task.FromException<int>(new OperationCanceledException()), Mapper));
        Assert.That(calls, Is.Zero);
    }

    [Test]
    public void MapperFailureAndUnrecognizedAsyncFailureStayVisible()
    {
        bool Broken(Exception _, out string error) { error = ""; throw new ApplicationException("mapper"); }
        Assert.Throws<ApplicationException>(() => Outcome.Try<int, string>(() => throw new IOException(), Broken));
        var unexpected = new InvalidOperationException("unexpected");
        Assert.That(Assert.ThrowsAsync<InvalidOperationException>(async () => await Outcome.TryAsync<int, string>(
            _ => Task.FromException<int>(unexpected), MapIo)), Is.SameAs(unexpected));
    }
}
