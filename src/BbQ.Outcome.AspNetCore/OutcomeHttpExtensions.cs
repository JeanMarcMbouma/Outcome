using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BbQ.Outcome.AspNetCore;

/// <summary>Converts only the active branch; success formatting remains endpoint-specific.</summary>
public static class OutcomeHttpExtensions
{
    /// <summary>Converts a typed outcome to an endpoint result using the configured error mapper.</summary>
    /// <example><code>
    /// return service.GetUser(id).ToIResult(httpMapper);
    /// </code></example>
    public static IResult ToIResult<T, TError>(this Outcome<T, TError> outcome,
        IOutcomeHttpMapper<TError> mapper, Func<T, IResult>? onSuccess = null)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return outcome.IsSuccess ? onSuccess is null ? TypedResults.Ok(outcome.Value) : onSuccess(outcome.Value)
            : TypedResults.Problem(mapper.Map(outcome.Errors));
    }

    /// <summary>Converts a heterogeneous outcome to an endpoint result using the configured error mapper.</summary>
    /// <example><code>
    /// return service.GetUser(id).ToIResult(httpMapper, user => TypedResults.Created($"/users/{user.Id}", user));
    /// </code></example>
    public static IResult ToIResult<T>(this Outcome<T> outcome,
        IOutcomeHttpMapper<object?> mapper, Func<T, IResult>? onSuccess = null)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return outcome.IsSuccess ? onSuccess is null ? TypedResults.Ok(outcome.Value) : onSuccess(outcome.Value)
            : TypedResults.Problem(mapper.Map(outcome.Errors));
    }

    /// <summary>Awaits a typed outcome and converts it to an endpoint result; cancellation only cancels the wait.</summary>
    /// <example><code>
    /// return await service.GetUserAsync(id, cancellationToken)
    ///     .ToIResultAsync(httpMapper, cancellationToken: cancellationToken);
    /// </code></example>
    public static async Task<IResult> ToIResultAsync<T, TError>(this Task<Outcome<T, TError>> task,
        IOutcomeHttpMapper<TError> mapper, Func<T, IResult>? onSuccess = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(mapper);
        var outcome = await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        return outcome.ToIResult(mapper, onSuccess);
    }

    /// <summary>Awaits a heterogeneous outcome and converts it to an endpoint result.</summary>
    /// <example><code>
    /// return await service.CreateAsync(command, cancellationToken)
    ///     .ToIResultAsync(httpMapper, user => TypedResults.Created($"/users/{user.Id}", user), cancellationToken);
    /// </code></example>
    public static async Task<IResult> ToIResultAsync<T>(this Task<Outcome<T>> task,
        IOutcomeHttpMapper<object?> mapper, Func<T, IResult>? onSuccess = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(mapper);
        var outcome = await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        return outcome.ToIResult(mapper, onSuccess);
    }

    /// <summary>
    /// Registers a scoped mapper and the ASP.NET Core Problem Details service. Register an
    /// IErrorDescriptorProvider for TError separately; scoped/localized providers are supported.
    /// </summary>
    /// <example><code>
    /// services.AddScoped&lt;IErrorDescriptorProvider&lt;AppError&gt;, AppErrorDescriptors&gt;();
    /// services.AddOutcomeHttpMapping&lt;AppError&gt;(options =>
    ///     options.Map("user.not_found", StatusCodes.Status404NotFound));
    /// </code></example>
    public static IServiceCollection AddOutcomeHttpMapping<TError>(this IServiceCollection services,
        Action<OutcomeHttpMappingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = new OutcomeHttpMappingOptions();
        configure?.Invoke(options);
        var snapshot = options.Snapshot();
        services.AddProblemDetails();
        services.TryAddScoped<IOutcomeHttpMapper<TError>>(provider =>
            new OutcomeHttpMapper<TError>(provider.GetRequiredService<IErrorDescriptorProvider<TError>>(), snapshot));
        return services;
    }
}
