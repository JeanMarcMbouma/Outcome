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
            return new Error<AppError>(AppError.UserNotFound, $"User '{request.Id}' not found").ToOutcome<Unit>();
        }

        var trimmed = request.NewName?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return new Error<AppError>(AppError.InvalidName, "New name must be non-empty").ToOutcome<Unit>();
        }

        await _repo.SaveAsync((id, trimmed!), ct);
        return new Unit();
    }
}
