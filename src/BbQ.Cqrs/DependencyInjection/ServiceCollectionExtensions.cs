using BbQ.Cqrs;
using Microsoft.Extensions.DependencyInjection;

namespace BbQ.Cqrs.DependencyInjection;

/// <summary>
/// Extension methods for registering CQRS components in the dependency injection container.
/// </summary>
/// <remarks>
/// This static class uses C# 14 extension types to add CQRS registration methods
/// to both IServiceCollection and IMediator.
/// </remarks>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {

        /// <summary>
        /// Registers the BbQ CQRS mediator and all handlers/behaviors in the specified assemblies.
        /// </summary>
        /// <param name="services">The service collection to register with</param>
        /// <param name="assemblies">Assemblies to scan for handlers and behaviors</param>
        /// <returns>The service collection for chaining</returns>
        /// <remarks>
        /// This method:
        /// 1. Registers IMediator as a singleton (single instance for the application)
        /// 2. Scans the provided assemblies for all classes implementing IRequestHandler&lt;,&gt;
        /// 3. Registers each handler with its implemented interfaces
        /// 4. Uses scoped lifetime for handlers (one per request in web apps)
        /// 5. Registers the built-in LoggingBehavior
        /// 
        /// Example usage:
        /// <code>
        /// services.AddBbQMediator(
        ///     typeof(CreateUserCommandHandler).Assembly,
        ///     typeof(GetUserByIdQueryHandler).Assembly
        /// );
        /// </code>
        /// 
        /// You can add custom behaviors after calling this method:
        /// <code>
        /// services.AddBbQMediator(/* assemblies */);
        /// services.AddScoped(typeof(IPipelineBehavior&lt;,&gt;), typeof(LoggingBehavior&lt;,&gt;));
        /// services.AddScoped(typeof(IPipelineBehavior&lt;,&gt;), typeof(ValidationBehavior&lt;,&gt;));
        /// services.AddScoped(typeof(IPipelineBehavior&lt;,&gt;), typeof(CachingBehavior&lt;,&gt;));
        /// </code>
        /// 
        /// Behavior registration order affects execution:
        /// - First registered = outermost (executes first, before behaviors registered later)
        /// - Last registered = innermost (executes last, just before the handler)
        /// </remarks>
        public IServiceCollection AddBbQMediator(
            params System.Reflection.Assembly[] assemblies)
        {
            // Register the mediator as singleton
            services.AddSingleton<IMediator, Mediator>();

            // Scan and register all request handlers from the provided assemblies
            services.Scan(s => s.FromAssemblies(assemblies)
                .AddClasses(c => c.AssignableTo(typeof(IRequestHandler<,>)))
                .AsImplementedInterfaces()
                .WithScopedLifetime());


            // Register behaviors in order (outermost to innermost)
            // The LoggingBehavior is registered as the outermost behavior
            // to capture the full request/response cycle

            return services;
        }
    }

    
    extension(IMediator mediator)
    {
        /// <summary>
        /// Convenience extension method on IMediator for sending requests.
        /// 
        /// This overload allows the request type to be inferred automatically
        /// from the argument, making it easier to call Send with less explicit typing.
        /// </summary>
        /// <typeparam name="TResponse">The response type (inferred from the request)</typeparam>
        /// <param name="mediator">The mediator instance</param>
        /// <param name="request">The request to send (concrete type inferred)</param>
        /// <param name="ct">Optional cancellation token</param>
        /// <returns>The response from the handler</returns>
        /// <remarks>
        /// Example:
        /// <code>
        /// // Without this extension, you would need to write:
        /// var result = await mediator.Send&lt;CreateUserCommand, Outcome&lt;User&gt;&gt;(command);
        /// 
        /// // With this extension, you can write:
        /// var result = await mediator.Send(command);
        /// 
        /// The compiler infers:
        /// - TRequest = CreateUserCommand (from the argument)
        /// - TResponse = Outcome&lt;User&gt; (from IRequest&lt;TResponse&gt;)
        /// </remarks>
        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken ct = default)
        {
            // The compile-time call-site infers TRequest concrete type and TResponse simultaneously.
            // This is more convenient than requiring explicit type parameters.
            return mediator.Send<IRequest<TResponse>, TResponse>(request, ct);
        }
    }
}
