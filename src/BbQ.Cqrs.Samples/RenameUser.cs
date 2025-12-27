using BbQ.Cqrs;
using BbQ.Outcome;

namespace BbQ.CQRS.Samples;

[Command]
public sealed record RenameUser(string Id, string NewName) : ICommand<Outcome<Unit>>;
