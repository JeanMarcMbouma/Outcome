using BbQ.Cqrs;
using BbQ.Cqrs.DependencyInjection;
using BbQ.Outcome;
using Microsoft.Extensions.DependencyInjection;

namespace BbQ.CQRS.Samples;

/// <summary>
/// Demonstrates how to use ICommandDispatcher and IQueryDispatcher
/// as an alternative to IMediator for dispatching commands and queries.
/// 
/// The dispatchers provide a more explicit separation between:
/// - Commands (state-changing operations) via ICommandDispatcher
/// - Queries (read-only operations) via IQueryDispatcher
/// </summary>
public static class DispatcherSample
{
    public static async Task RunExample()
    {
        Console.WriteLine("=== Dispatcher Sample ===\n");

        // Setup dependency injection
        var services = new ServiceCollection();
        services.AddBbQMediator(typeof(DispatcherSample).Assembly);
        
        // Register handlers
        services.AddTransient<IRequestHandler<CreateUserCommand, Outcome<User>>, CreateUserCommandHandler>();
        services.AddTransient<IRequestHandler<GetUserQuery, Outcome<User>>, GetUserQueryHandler>();

        var serviceProvider = services.BuildServiceProvider();

        // Get the dispatchers
        var commandDispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();
        var queryDispatcher = serviceProvider.GetRequiredService<IQueryDispatcher>();

        // Example 1: Using CommandDispatcher
        Console.WriteLine("--- Example 1: Using ICommandDispatcher ---");
        var createCommand = new CreateUserCommand("john@example.com", "John Doe");
        var createResult = await commandDispatcher.Dispatch(createCommand);

        createResult.Switch(
            onSuccess: user => Console.WriteLine($"✓ User created: {user.Name} ({user.Email})"),
            onError: errors => Console.WriteLine($"✗ Error: {errors.Count} error(s) occurred")
        );

        // Example 2: Using QueryDispatcher
        Console.WriteLine("\n--- Example 2: Using IQueryDispatcher ---");
        var getQuery = new GetUserQuery("john@example.com");
        var getResult = await queryDispatcher.Dispatch(getQuery);

        getResult.Switch(
            onSuccess: user => Console.WriteLine($"✓ User retrieved: {user.Name} ({user.Email})"),
            onError: errors => Console.WriteLine($"✗ Error: {errors.Count} error(s) occurred")
        );

        // Example 3: Benefits of separate dispatchers
        Console.WriteLine("\n--- Benefits of Separate Dispatchers ---");
        Console.WriteLine("• ICommandDispatcher explicitly handles state-changing operations");
        Console.WriteLine("• IQueryDispatcher explicitly handles read-only operations");
        Console.WriteLine("• Clear separation of concerns at the API level");
        Console.WriteLine("• Better discoverability and documentation");
        Console.WriteLine("• Type safety with compile-time checking");
        Console.WriteLine("• No runtime scanning or magic – uses dependency injection with minimal reflection only to build and cache pipelines on first use");

        serviceProvider.Dispose();
    }
}

// Sample command and query types
public record CreateUserCommand(string Email, string Name) : ICommand<Outcome<User>>;

public record GetUserQuery(string Email) : IQuery<Outcome<User>>;

public record User(string Email, string Name);

// Sample handlers
public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Outcome<User>>
{
    public Task<Outcome<User>> Handle(CreateUserCommand request, CancellationToken ct)
    {
        // Simulate creating a user
        var user = new User(request.Email, request.Name);
        return Task.FromResult(Outcome<User>.From(user));
    }
}

public class GetUserQueryHandler : IRequestHandler<GetUserQuery, Outcome<User>>
{
    public Task<Outcome<User>> Handle(GetUserQuery request, CancellationToken ct)
    {
        // Simulate retrieving a user
        var user = new User(request.Email, "John Doe");
        return Task.FromResult(Outcome<User>.From(user));
    }
}
