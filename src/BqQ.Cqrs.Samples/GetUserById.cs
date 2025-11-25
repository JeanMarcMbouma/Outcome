using BbQ.Cqrs;
using BbQ.Outcome;

namespace BqQ.CQRS.Samples;

// Example requests
public sealed record GetUserById(string Id) : IQuery<Outcome<UserDto>>;
