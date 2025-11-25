// -------------------------------
// Core contracts (Outcome-centric)
// -------------------------------
namespace BbQ.Cqrs;

public interface IQuery<TResponse> : IRequest<TResponse> { }
