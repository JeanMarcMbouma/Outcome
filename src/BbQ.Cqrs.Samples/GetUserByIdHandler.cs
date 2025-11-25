using BbQ.Cqrs;
using BbQ.Outcome;

namespace BbQ.CQRS.Samples;

public sealed class GetUserByIdHandler
    : IRequestHandler<GetUserById, Outcome<UserDto>>
{
    private readonly IUserRepository _repo;
    public GetUserByIdHandler(IUserRepository repo) => _repo = repo;

    public async Task<Outcome<UserDto>> Handle(GetUserById request, CancellationToken ct)
    {
        var (found, id, name) = await _repo.FindAsync(request.Id, ct);
        if (!found)
        {
           return new Error<AppError>(AppError.UserNotFound, $"User '{request.Id}' not found").ToOutcome<UserDto>();
        }

        return new UserDto(id, name);
    }
}
