// -------------------------------
// Core contracts (Outcome-centric)
// -------------------------------
namespace BbQ.Cqrs;

/// <summary>
/// A simple sentinel value type used to represent "no return value" in requests.
/// 
/// This is analogous to "void" but as a generic type parameter, allowing
/// IRequest implementations to return a concrete type instead of nothing.
/// Enables the mediator to treat all requests uniformly with a TResponse.
/// </summary>
/// <remarks>
/// Used by IRequest (without type parameter) to represent void-returning requests.
/// Handlers implementing IRequestHandler&lt;TRequest&gt; (without TResponse) 
/// work with Unit internally.
/// 
/// Example:
/// <code>
/// // A request that doesn't return data (void-like)
/// public class SendEmailCommand : IRequest
/// {
///     public string Email { get; set; }
///     public string Subject { get; set; }
/// }
/// 
/// // The handler doesn't need to return anything meaningful
/// public class SendEmailCommandHandler : IRequestHandler&lt;SendEmailCommand&gt;
/// {
///     public async Task Handle(SendEmailCommand request, CancellationToken ct)
///     {
///         await _emailService.SendAsync(request.Email, request.Subject, ct);
///         // No return value needed
///     }
/// }
/// 
/// // When sent through the mediator:
/// await mediator.Send(new SendEmailCommand { Email = "...", Subject = "..." });
/// // Result is Unit (discarded by caller)
/// </code>
/// </remarks>
public readonly record struct Unit
{
    /// <summary>
    /// The singleton instance of Unit.
    /// </summary>
    public static readonly Unit Value = new();
}