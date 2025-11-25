using BbQ.Cqrs;
using BbQ.Outcome;

namespace BqQ.CQRS.Samples;

public sealed record RenameUser(string Id, string NewName) : ICommand<Outcome<Unit>>;
