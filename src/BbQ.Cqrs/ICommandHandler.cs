// -------------------------------
// Core contracts (Outcome-centric)
// -------------------------------
namespace BbQ.Cqrs;

/// <summary>
/// Defines a handler for a command that returns a response.
/// </summary>
/// <typeparam name="TCommand">
/// The type of the command being handled. Must implement <see cref="ICommand{TResponse}"/>.
/// </typeparam>
/// <typeparam name="TResponse">
/// The type of the response returned after executing the command.
/// </typeparam>
/// <remarks>
/// This interface extends <see cref="IRequestHandler{TCommand, TResponse}"/> from MediatR,
/// enabling commands to participate in the MediatR pipeline.
/// </remarks>
/// <example>
/// <code>
/// public sealed record CreateOrderCommand(Guid CustomerId, decimal Amount)
///     : ICommand<OrderDto>;
///
/// public sealed class CreateOrderHandler
///     : ICommandHandler<CreateOrderCommand, OrderDto>
/// {
///     private readonly IOrderService _service;
///
///     public CreateOrderHandler(IOrderService service)
///     {
///         _service = service;
///     }
///
///     public async Task<OrderDto> Handle(CreateOrderCommand command, CancellationToken ct)
///     {
///         var order = await _service.CreateAsync(command.CustomerId, command.Amount, ct);
///         return new OrderDto(order.Id, order.Total);
///     }
/// }
/// </code>
/// </example>
/// <example>
/// <code>
/// // Dispatching a command with a response
/// var order = await _mediator.Send(new CreateOrderCommand(customerId, 99.50m));
/// Console.WriteLine(order.Id);
/// </code>
/// </example>
public interface ICommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>;


/// <summary>
/// Defines a handler for a command that does not return a response.
/// </summary>
/// <typeparam name="TCommand">
/// The type of the command being handled. Must implement <see cref="ICommand"/>.
/// </typeparam>
/// <remarks>
/// This interface maps commands to <see cref="Unit"/> responses for void-returning operations.
/// </remarks>
/// <example>
/// <code>
/// public sealed record DeleteUserCommand(Guid UserId) : ICommand;
///
/// public sealed class DeleteUserHandler
///     : ICommandHandler<DeleteUserCommand>
/// {
///     private readonly IUserRepository _repository;
///
///     public DeleteUserHandler(IUserRepository repository)
///     {
///         _repository = repository;
///     }
///
///     public async Task<Unit> Handle(DeleteUserCommand command, CancellationToken ct)
///     {
///         await _repository.DeleteAsync(command.UserId, ct);
///         return Unit.Value;
///     }
/// }
/// </code>
/// </example>
/// <example>
/// <code>
/// // Dispatching a void-returning command
/// await _mediator.Send(new DeleteUserCommand(userId));
/// </code>
/// </example>
public interface ICommandHandler<TCommand> : IRequestHandler<TCommand>
    where TCommand : ICommand, IRequest;
