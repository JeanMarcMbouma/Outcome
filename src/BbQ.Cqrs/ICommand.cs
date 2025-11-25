// -------------------------------
// Core contracts (Outcome-centric)
// -------------------------------
namespace BbQ.Cqrs;

public interface ICommand<TResponse> : IRequest<TResponse> { }
