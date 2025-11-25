namespace BbQ.CQRS.Samples;

// -------------------
// Example handlers
// -------------------
public interface IUserRepository
{
    Task<(bool Found, string Id, string Name)> FindAsync(string id, CancellationToken ct);
    Task SaveAsync((string Id, string Name) user, CancellationToken ct);
}
