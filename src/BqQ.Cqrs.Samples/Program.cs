using BbQ.Cqrs;
using BbQ.Cqrs.Testing;
using BbQ.Outcome;
using System.Diagnostics;

namespace BqQ.CQRS.Samples;

static class Program
{
    static async Task Main()
    {
        var handler = new BbQ.Cqrs.Testing.StubHandler<GetUserById, Outcome<UserDto>>(
        async (req, ct) => AppErrorErrors.TransientError.ToOutcome<UserDto>());

        var retry = new RetryBehavior<GetUserById, Outcome<UserDto>, UserDto>(maxAttempts: 2);
        var mediator = new TestMediator<GetUserById, Outcome<UserDto>>(handler, new[] { retry });

        // Act
        var outcome = await mediator.Send(new GetUserById("42"));

        // Assert
        var (ok, _, errors) = outcome;
        Debug.Assert(!ok);
        Debug.Assert(errors!.OfType<Error<AppError>>().Any(e => e.Code == AppError.Transient));
    }
}
