using BbQ.Cqrs;
using BbQ.Outcome;

namespace BbQ.CQRS.Samples;

public interface IRequestValidator<TRequest>
{
    Task<(bool IsValid, string Description)> ValidateAsync(TRequest request, CancellationToken ct);
}


public sealed class RenameUserValidator : IRequestValidator<RenameUser>
{
    public Task<(bool IsValid, string Description)> ValidateAsync(RenameUser request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NewName))
            return Task.FromResult((false, "New name must be non-empty"));

        if (request.NewName.Length > 50)
            return Task.FromResult((false, "New name must be at most 50 characters"));

        return Task.FromResult((true, string.Empty));
    }
}

// Note: ValidationBehavior has 3 type parameters (TRequest, TResponse, TPayload) which makes it
// incompatible with the source generator's automatic registration. Behaviors with more than 2
// type parameters must be registered manually. The [Behavior] attribute should only be used on
// behaviors that directly match IPipelineBehavior<TRequest, TResponse> with exactly 2 type parameters.
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
            var failure = Outcome<TPayload>.FromError(new Error<AppError>(AppError.InvalidName, result.Description)) as IOutcome<TPayload>;
            return (TResponse)failure;
        }

        return await next(request, ct);
    }
}
