// -------------------------------
// Core contracts (Outcome-centric)
// -------------------------------
namespace BbQ.Cqrs;

/// <summary>
/// Base marker interface for all requests in the CQRS pipeline.
/// 
/// Represents a single operation (command or query) that will be sent through
/// the mediator to be handled by an IRequestHandler&lt;TRequest, TResponse&gt;.
/// </summary>
/// <typeparam name="TResponse">The type of response expected from handling this request</typeparam>
/// <remarks>
/// This interface serves as the constraint for the mediator's generic Send method.
/// Implement either ICommand&lt;TResponse&gt; or IQuery&lt;TResponse&gt; instead of 
/// directly implementing this interface.
/// 
/// Example:
/// <code>
/// public class GetUserByIdQuery : IQuery&lt;User&gt; { }
/// public class CreateUserCommand : ICommand&lt;Outcome&lt;User&gt;&gt; { }
/// </code>
/// </remarks>
public interface IRequest<TResponse> { }
