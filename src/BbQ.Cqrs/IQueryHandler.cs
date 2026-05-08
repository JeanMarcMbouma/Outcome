// -------------------------------
// Core contracts (Outcome-centric)
// -------------------------------
namespace BbQ.Cqrs;

/// <summary>
/// Defines a handler for a query that returns a response.
/// </summary>
/// <typeparam name="TQuery">
/// The type of the query being handled. Must implement <see cref="IQuery{TResponse}"/>.
/// </typeparam>
/// <typeparam name="TResponse">
/// The type of the response returned after handling the query.
/// </typeparam>
/// <remarks>
/// This interface extends <see cref="IRequestHandler{TQuery, TResponse}"/> from MediatR,
/// enabling queries to be processed using the MediatR pipeline.
/// </remarks>
/// <example>
/// <code>
/// public sealed record GetUserByIdQuery(Guid Id) : IQuery<UserDto>;
///
/// public sealed class GetUserByIdHandler 
///     : IQueryHandler<GetUserByIdQuery, UserDto>
/// {
///     private readonly IUserRepository _repository;
///
///     public GetUserByIdHandler(IUserRepository repository)
///     {
///         _repository = repository;
///     }
///
///     public async Task<UserDto> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
///     {
///         var user = await _repository.GetByIdAsync(query.Id, cancellationToken);
///         return new UserDto(user.Id, user.Name);
///     }
/// }
/// </code>
/// </example>
/// <example>
/// <code>
/// // Dispatching a query using MediatR
/// var result = await _mediator.Send(new GetUserByIdQuery(userId));
/// Console.WriteLine(result.Name);
/// </code>
/// </example>
public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>;
