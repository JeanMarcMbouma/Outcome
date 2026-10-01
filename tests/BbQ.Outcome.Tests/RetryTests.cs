using NUnit.Framework;

namespace BbQ.Outcome.Tests;

[TestFixture]
public sealed class RetryTests
{
    [Test]
    public async Task RetryAsyncReturnsFirstSuccessWithoutRetrying()
    {
        var attempts = 0;

        var result = await Outcome.RetryAsync<int, string>(
            _ =>
            {
                attempts++;
                return Task.FromResult(Outcome<int, string>.From(42));
            },
            _ => true);

        Assert.That(result.Value, Is.EqualTo(42));
        Assert.That(attempts, Is.EqualTo(1));
    }

    [Test]
    public async Task RetryAsyncEventuallyReturnsSuccess()
    {
        var attempts = 0;

        var result = await Outcome.RetryAsync<int, string>(
            _ =>
            {
                attempts++;
                return Task.FromResult(
                    attempts < 3
                        ? Outcome<int, string>.FromError("TRANSIENT")
                        : Outcome<int, string>.From(42));
            },
            errors => errors.All(error => error == "TRANSIENT"),
            new RetryOptions { MaxAttempts = 3 });

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(42));
        Assert.That(attempts, Is.EqualTo(3));
    }

    [Test]
    public async Task RetryAsyncStopsOnNonRetryableFailure()
    {
        var attempts = 0;

        var result = await Outcome.RetryAsync<int, string>(
            _ =>
            {
                attempts++;
                return Task.FromResult(Outcome<int, string>.FromError("PERMANENT"));
            },
            errors => errors.All(error => error == "TRANSIENT"),
            new RetryOptions { MaxAttempts = 5 });

        Assert.That(result.IsError, Is.True);
        Assert.That(result.Errors, Does.Contain("PERMANENT"));
        Assert.That(attempts, Is.EqualTo(1));
    }

    [Test]
    public async Task RetryAsyncReturnsFinalFailureWhenAttemptsAreExhausted()
    {
        var attempts = 0;

        var result = await Outcome.RetryAsync<int, string>(
            _ =>
            {
                attempts++;
                return Task.FromResult(Outcome<int, string>.FromError($"failure-{attempts}"));
            },
            _ => true,
            new RetryOptions { MaxAttempts = 3 });

        Assert.That(attempts, Is.EqualTo(3));
        Assert.That(result.Errors[0], Is.EqualTo("failure-3"));
    }

    [Test]
    public void RetryAsyncPropagatesCancellationDuringDelay()
    {
        using var cancellation = new CancellationTokenSource();
        var attempts = 0;

        Assert.CatchAsync<OperationCanceledException>(async () =>
            await Outcome.RetryAsync<int, string>(
                _ =>
                {
                    attempts++;
                    cancellation.Cancel();
                    return Task.FromResult(Outcome<int, string>.FromError("TRANSIENT"));
                },
                _ => true,
                new RetryOptions
                {
                    MaxAttempts = 2,
                    DelayGenerator = _ => TimeSpan.FromSeconds(30)
                },
                cancellation.Token));

        Assert.That(attempts, Is.EqualTo(1));
    }

    [Test]
    public void RetryAsyncDoesNotCatchUnexpectedExceptions()
    {
        var exception = new InvalidOperationException("bug");

        var thrown = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Outcome.RetryAsync<int, string>(
                _ => Task.FromException<Outcome<int, string>>(exception),
                _ => true));

        Assert.That(thrown, Is.SameAs(exception));
    }

    [Test]
    public async Task RetryAsyncComposesWithTryAsync()
    {
        var attempts = 0;

        static bool MapTimeout(Exception exception, out string error)
        {
            error = "TIMEOUT";
            return exception is TimeoutException;
        }

        var result = await Outcome.RetryAsync<int, string>(
            ct => Outcome.TryAsync<int, string>(
                _ =>
                {
                    attempts++;
                    return attempts < 3
                        ? Task.FromException<int>(new TimeoutException())
                        : Task.FromResult(42);
                },
                MapTimeout,
                ct),
            errors => errors.All(error => error == "TIMEOUT"),
            new RetryOptions { MaxAttempts = 3 });

        Assert.That(result.Value, Is.EqualTo(42));
        Assert.That(attempts, Is.EqualTo(3));
    }

    [Test]
    public async Task HeterogeneousRetryAsyncRetriesSelectedErrors()
    {
        var attempts = 0;

        var result = await Outcome.RetryAsync<int>(
            _ =>
            {
                attempts++;
                return Task.FromResult(
                    attempts == 1
                        ? Outcome<int>.FromErrors(new object?[] { "TRANSIENT" })
                        : Outcome<int>.From(42));
            },
            errors => errors.All(error => Equals(error, "TRANSIENT")),
            new RetryOptions { MaxAttempts = 2 });

        Assert.That(result.Value, Is.EqualTo(42));
        Assert.That(attempts, Is.EqualTo(2));
    }

    [Test]
    public void RetryAsyncRejectsInvalidAttemptCount()
    {
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await Outcome.RetryAsync<int, string>(
                _ => Task.FromResult(Outcome<int, string>.From(42)),
                _ => true,
                new RetryOptions { MaxAttempts = 0 }));
    }

    [Test]
    public void RetryAsyncRejectsNegativeGeneratedDelay()
    {
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Outcome.RetryAsync<int, string>(
                _ => Task.FromResult(Outcome<int, string>.FromError("TRANSIENT")),
                _ => true,
                new RetryOptions
                {
                    MaxAttempts = 2,
                    DelayGenerator = _ => TimeSpan.FromMilliseconds(-1)
                }));
    }
}
