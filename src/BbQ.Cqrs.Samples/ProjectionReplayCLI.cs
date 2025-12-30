// Example: Projection Replay CLI Tool
// This demonstrates how to use IProjectionRebuilder in a CLI application
// to reset and rebuild projections.

using BbQ.Events;
using BbQ.Events.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BbQ.Cqrs.Samples;

/// <summary>
/// Sample CLI tool demonstrating projection replay functionality.
/// </summary>
public static class ProjectionReplayCLI
{
    /// <summary>
    /// Example entry point for a projection replay CLI tool.
    /// </summary>
    public static async Task RunAsync(string[] args)
    {
        // Configure services
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddInMemoryEventBus();
        
        // Register your projections
        // services.AddProjectionsFromAssembly(typeof(Program).Assembly);
        
        services.AddProjectionEngine();

        var provider = services.BuildServiceProvider();
        var rebuilder = provider.GetRequiredService<IProjectionRebuilder>();

        // Parse command-line arguments
        if (args.Length == 0 || args[0] == "list")
        {
            // List all projections
            Console.WriteLine("Registered projections:");
            var projections = rebuilder.GetRegisteredProjections().ToList();
            
            if (projections.Count == 0)
            {
                Console.WriteLine("  (no projections registered)");
            }
            else
            {
                foreach (var projection in projections)
                {
                    Console.WriteLine($"  - {projection}");
                }
            }
        }
        else if (args[0] == "reset-all")
        {
            // Reset all projections
            Console.WriteLine("Resetting all projections...");
            await rebuilder.ResetAllProjectionsAsync();
            Console.WriteLine("✓ All projections reset successfully.");
            Console.WriteLine("  Restart the projection engine to rebuild from scratch.");
        }
        else if (args[0] == "reset" && args.Length >= 2)
        {
            // Reset a specific projection
            var projectionName = args[1];
            Console.WriteLine($"Resetting projection: {projectionName}...");
            
            try
            {
                await rebuilder.ResetProjectionAsync(projectionName);
                Console.WriteLine($"✓ Projection '{projectionName}' reset successfully.");
                Console.WriteLine("  Restart the projection engine to rebuild from scratch.");
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"✗ Error: {ex.Message}");
            }
        }
        else if (args[0] == "reset-partition" && args.Length >= 3)
        {
            // Reset a specific partition
            var projectionName = args[1];
            var partitionKey = args[2];
            Console.WriteLine($"Resetting partition '{partitionKey}' of projection '{projectionName}'...");
            
            try
            {
                await rebuilder.ResetPartitionAsync(projectionName, partitionKey);
                Console.WriteLine($"✓ Partition '{partitionKey}' of projection '{projectionName}' reset successfully.");
                Console.WriteLine("  Restart the projection engine to rebuild this partition from scratch.");
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"✗ Error: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("Projection Replay CLI Tool");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  list                                                - List all registered projections");
            Console.WriteLine("  reset-all                                           - Reset all projections");
            Console.WriteLine("  reset <projection-name>                             - Reset a specific projection");
            Console.WriteLine("  reset-partition <projection-name> <partition-key>   - Reset a specific partition");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  dotnet run list");
            Console.WriteLine("  dotnet run reset-all");
            Console.WriteLine("  dotnet run reset UserProfileProjection");
            Console.WriteLine("  dotnet run reset-partition UserStatisticsProjection user-123");
        }

        await provider.DisposeAsync();
    }
}

// Example projections for demonstration
public record UserCreatedEvent(string UserId, string Name, string Email);
public record UserActivityEvent(string UserId, string ActivityType, DateTime Timestamp);

[Projection]
public class ExampleUserProfileProjection : IProjectionHandler<UserCreatedEvent>
{
    public ValueTask ProjectAsync(UserCreatedEvent @event, CancellationToken ct = default)
    {
        // Build user profile read model
        Console.WriteLine($"Processing UserCreated: {@event.UserId}");
        return ValueTask.CompletedTask;
    }
}

[Projection]
public class ExampleUserActivityProjection : IPartitionedProjectionHandler<UserActivityEvent>
{
    public string GetPartitionKey(UserActivityEvent @event) => @event.UserId;

    public ValueTask ProjectAsync(UserActivityEvent @event, CancellationToken ct = default)
    {
        // Build user activity statistics
        Console.WriteLine($"Processing UserActivity for user {@event.UserId}: {@event.ActivityType}");
        return ValueTask.CompletedTask;
    }
}
