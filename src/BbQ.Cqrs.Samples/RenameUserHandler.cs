using BbQ.Cqrs;
using BbQ.Outcome;

namespace BbQ.CQRS.Samples;

public sealed class RenameUserHandler
    : IRequestHandler<RenameUser, Outcome<Unit>>
{
    private readonly IUserRepository _repo;
    public RenameUserHandler(IUserRepository repo) => _repo = repo;

    public async Task<Outcome<Unit>> Handle(RenameUser request, CancellationToken ct)
    {
        var (found, id, name) = await _repo.FindAsync(request.Id, ct);
        if (!found)
        {
            return Outcome<Unit>.FromError(new Error<AppError>(AppError.UserNotFound, $"User '{request.Id}' not found"));
        }

        var trimmed = request.NewName?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return Outcome<Unit>.FromError(new Error<AppError>(AppError.InvalidName, "New name must be non-empty"));
        }

        await _repo.SaveAsync((id, trimmed!), ct);
        return new Unit();
    }
}
