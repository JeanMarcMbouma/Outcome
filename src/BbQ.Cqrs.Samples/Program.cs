using BbQ.Cqrs;
using BbQ.Cqrs.Testing;
using BbQ.Outcome;
using System.Diagnostics;

namespace BbQ.CQRS.Samples;

static class Program
{
    static async Task Main()
    {
        var handler = new BbQ.Cqrs.Testing.StubHandler<GetUserById, Outcome<UserDto>>(
        async (req, ct) => Outcome<UserDto>.FromError(AppErrorErrors.TransientError));

        var retry = new RetryBehavior<GetUserById, Outcome<UserDto>, UserDto>(maxAttempts: 2);
        var mediator = new TestMediator<GetUserById, Outcome<UserDto>>(handler, new[] { retry });

        // Act
        var outcome = await mediator.Send(new GetUserById("42"));

        // Assert
        var (ok, _, errors) = outcome;
        Debug.Assert(!ok);
        Debug.Assert(errors!.OfType<Error<AppError>>().Any(e => e.Code == AppError.Transient));

        Console.WriteLine($"Errors: {string.Join(", ", errors!.Select(e => e))}");
    }
}
