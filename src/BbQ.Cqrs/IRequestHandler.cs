// -------------------------------
// Core contracts (Outcome-centric)
// -------------------------------
namespace BbQ.Cqrs;

/// <summary>
/// Handler contract for processing requests in the CQRS pipeline.
/// 
/// Implementations handle a specific request type and produce a response.
/// The handler is the terminal point in the pipeline, after all behaviors
/// have executed.
/// </summary>
/// <typeparam name="TRequest">The request type to handle, must implement IRequest&lt;TResponse&gt;</typeparam>
/// <typeparam name="TResponse">The response type produced by this handler</typeparam>
/// <remarks>
/// Implementation guidelines:
/// - Each request type should have exactly one handler
/// - Handlers should be registered with the service container (AddBbQMediator)
/// - Handlers receive cancellation tokens to support graceful cancellation
/// - Use dependency injection to access repositories, services, validators, etc.
/// 
/// Example handler:
/// <code>
/// public class CreateUserCommandHandler : IRequestHandler&lt;CreateUserCommand, Outcome&lt;User&gt;&gt;
/// {
///     private readonly IUserRepository _repository;
///     private readonly IValidator&lt;CreateUserCommand&gt; _validator;
///     
///     public CreateUserCommandHandler(
///         IUserRepository repository,
///         IValidator&lt;CreateUserCommand&gt; validator)
///     {
///         _repository = repository;
///         _validator = validator;
///     }
///     
///     public async Task&lt;Outcome&lt;User&gt;&gt; Handle(CreateUserCommand request, CancellationToken ct)
///     {
///         // Validate the request
///         var validation = await _validator.ValidateAsync(request, ct);
///         if (!validation.IsValid)
///         {
///             return Outcome&lt;User&gt;.Validation("INVALID_USER", "User data is invalid");
///         }
///         
///         // Handle the command
///         var user = new User { Email = request.Email, Name = request.Name };
///         await _repository.AddAsync(user, ct);
///         
///         return Outcome&lt;User&gt;.From(user);
///     }
/// }
/// </code>
/// </remarks>
public interface IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles the request and returns a response.
    /// </summary>
    /// <param name="request">The request to process</param>
    /// <param name="ct">Cancellation token for async operations</param>
    /// <returns>The response from processing the request</returns>
    /// <remarks>
    /// This method is called at the end of the pipeline after all behaviors
    /// have been executed. It should contain the core business logic for
    /// handling the request.
    /// </remarks>
    Task<TResponse> Handle(TRequest request, CancellationToken ct);
}
