using BqQ.Cqrs;
using Microsoft.Extensions.DependencyInjection;

namespace BbQ.Cqrs.DependencyInjection;


public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddOutcomeMediator(
        params System.Reflection.Assembly[] assemblies)
        {
            services.AddSingleton<IMediator, Mediator>();

            // Handlers: scan and register
            services.Scan(s => s.FromAssemblies(assemblies)
                .AddClasses(c => c.AssignableTo(typeof(IRequestHandler<,>)))
                .AsImplementedInterfaces()
                .WithScopedLifetime());

            // Behaviors: registration order = execution order (outermost first)
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            // Add closed generic registrations for behaviors with payload typing where desired:
            // Example: ValidationBehavior<GetUserById, Outcome<UserDto>, UserDto>
            // You can also use Scrutor to register all closed versions if you have validators per request.

            return services;
        }
    }

    extension(IMediator mediator)
    {
        // Convenience Send overload to infer TRequest automatically
        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken ct = default)
        {
            // The compile-time call-site will infer TRequest concrete type and TResponse simultaneously.
            return mediator.Send<IRequest<TResponse>, TResponse>(request, ct);
        }
    }
}
