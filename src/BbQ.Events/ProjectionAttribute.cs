namespace BbQ.Events;

/// <summary>
/// Marks a class as a projection for automatic registration and discovery.
/// Only classes with this attribute are automatically registered by the source generator.
/// </summary>
/// <remarks>
/// The Projection attribute:
/// - Enables automatic discovery of projection handlers
/// - Works with source generators to generate registration code
/// - Requires the class to implement at least one IProjectionHandler&lt;TEvent&gt; or IPartitionedProjectionHandler&lt;TEvent&gt;
/// 
/// Usage:
/// <code>
/// [Projection]
/// public class UserProfileProjection :
///     IProjectionHandler&lt;UserCreated&gt;,
///     IProjectionHandler&lt;UserUpdated&gt;
/// {
///     // Implementation...
/// }
/// 
/// [Projection]
/// public class UserStatisticsProjection : IPartitionedProjectionHandler&lt;UserActivity&gt;
/// {
///     // Implementation...
/// }
/// </code>
/// 
/// Projections are automatically registered when using:
/// <code>
/// services.AddProjectionsFromAssembly(typeof(Program).Assembly);
/// </code>
/// 
/// Or manually registered:
/// <code>
/// services.AddInMemoryEventBus();
/// services.AddProjection&lt;UserProfileProjection&gt;();
/// services.AddProjectionEngine();
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ProjectionAttribute : Attribute
{
}
