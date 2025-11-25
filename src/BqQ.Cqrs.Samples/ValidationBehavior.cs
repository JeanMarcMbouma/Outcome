using BbQ.Cqrs;
using BbQ.Outcome;

namespace BqQ.CQRS.Samples;

public interface IRequestValidator<TRequest>
{
    Task<(bool IsValid, string Description)> ValidateAsync(TRequest request, CancellationToken ct);
}

public sealed class ValidationBehavior<TRequest, TResponse, TPayload>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IOutcome<TPayload>
{
    private readonly IRequestValidator<TRequest> _validator;

    public ValidationBehavior(IRequestValidator<TRequest> validator) => _validator = validator;

    public async Task<TResponse> Handle(
        TRequest request,
        CancellationToken ct,
        Func<TRequest, CancellationToken, Task<TResponse>> next)
    {
        var result = await _validator.ValidateAsync(request, ct);
        if (!result.IsValid)
        {
            // Construct Outcome<TPayload> failure directly with your API (no reflection)
            var failure = new Error<AppError>(AppError.InvalidName, result.Description).ToOutcome<TPayload>() as IOutcome<TPayload>;
            return (TResponse)failure;
        }

        return await next(request, ct);
    }
}
