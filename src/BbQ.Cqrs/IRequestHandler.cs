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

/// <summary>
/// Handler contract for fire-and-forget requests that don't return a meaningful value.
/// 
/// Implements the IRequest (without TResponse) pattern for operations
/// like sending notifications, publishing events, or executing commands
/// where the return value is not important.
/// </summary>
/// <typeparam name="TRequest">The request type, must implement IRequest</typeparam>
/// <remarks>
/// Implementation guidelines:
/// - Use this for operations that don't need a return value (void-like)
/// - Each request type should have exactly one handler
/// - Handlers should be registered with the service container (AddBbQMediator)
/// - The Handle method doesn't need to return a value; the framework handles Unit internally
/// 
/// Example handler:
/// <code>
/// public class SendEmailCommandHandler : IRequestHandler&lt;SendEmailCommand&gt;
/// {
///     private readonly IEmailService _emailService;
///     
///     public SendEmailCommandHandler(IEmailService emailService)
///     {
///         _emailService = emailService;
///     }
///     
///     public async Task Handle(SendEmailCommand request, CancellationToken ct)
///     {
///         // Perform the action without returning a meaningful result
///         await _emailService.SendAsync(request.Email, request.Subject, request.Body, ct);
///         // No return value needed
///     }
/// }
/// 
/// // Usage in a controller or service
/// public class UserService
/// {
///     private readonly IMediator _mediator;
///     
///     public async Task RegisterUserAsync(string email, CancellationToken ct)
///     {
///         // ... create user ...
///         
///         // Send notification (fire-and-forget)
///         await _mediator.Send(new SendWelcomeEmailCommand { Email = email }, ct);
///     }
/// }
/// </code>
/// </remarks>
public interface IRequestHandler<TRequest>
    where TRequest: IRequest
{
    /// <summary>
    /// Handles the request without returning a meaningful value.
    /// </summary>
    /// <param name="request">The request to process</param>
    /// <param name="ct">Cancellation token for async operations</param>
    /// <remarks>
    /// This method is called at the end of the pipeline after all behaviors
    /// have been executed. It should perform its side effects without
    /// needing to return data to the caller.
    /// </remarks>
    Task Handle(TRequest request, CancellationToken ct);
}