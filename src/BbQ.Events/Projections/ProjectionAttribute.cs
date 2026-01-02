using BbQ.Events.Engine;

namespace BbQ.Events.Projections;

/// <summary>
/// Marks a class as a projection for automatic registration and discovery.
/// Only classes with this attribute are automatically registered by the source generator.
/// </summary>
/// <remarks>
/// The Projection attribute:
/// - Enables automatic discovery of projection handlers
/// - Works with source generators to generate registration code
/// - Requires the class to implement at least one IProjectionHandler&lt;TEvent&gt; or IPartitionedProjectionHandler&lt;TEvent&gt;
/// - Optionally configures projection behavior via MaxDegreeOfParallelism, CheckpointBatchSize, and StartupMode
/// 
/// **IMPORTANT:** When manually registering projections (not using source generators), prefer configuring 
/// options via the AddProjection overload that accepts a configuration lambda instead of using this attribute:
/// 
/// <code>
/// // Preferred for manual registration
/// services.AddProjection&lt;UserProfileProjection&gt;(options =&gt; 
/// {
///     options.MaxDegreeOfParallelism = 4;
///     options.CheckpointBatchSize = 50;
///     options.StartupMode = ProjectionStartupMode.Replay;
/// });
/// 
/// // Using attribute is fine when projections are discovered by source generators
/// </code>
/// 
/// Usage with source generators:
/// <code>
/// [Projection]
/// public class UserProfileProjection :
///     IProjectionHandler&lt;UserCreated&gt;,
///     IProjectionHandler&lt;UserUpdated&gt;
/// {
///     // Implementation...
/// }
/// 
/// [Projection(MaxDegreeOfParallelism = 4, CheckpointBatchSize = 50, StartupMode = ProjectionStartupMode.LiveOnly)]
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
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ProjectionAttribute : Attribute
{
    /// <summary>
    /// Maximum number of partitions that can be processed in parallel.
    /// Default: 1 (sequential processing)
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = 1;

    /// <summary>
    /// Number of events to process before persisting a checkpoint.
    /// Default: 100
    /// </summary>
    public int CheckpointBatchSize { get; set; } = 100;

    /// <summary>
    /// Defines how the projection should start processing events.
    /// Default: ProjectionStartupMode.Resume
    /// </summary>
    public ProjectionStartupMode StartupMode { get; set; } = ProjectionStartupMode.Resume;

    /// <summary>
    /// Maximum number of events that can be queued per partition before backpressure is applied.
    /// Default: 1000
    /// </summary>
    public int ChannelCapacity { get; set; } = 1000;

    /// <summary>
    /// Strategy for handling backpressure when event queue reaches capacity.
    /// Default: BackpressureStrategy.Block
    /// </summary>
    public BackpressureStrategy BackpressureStrategy { get; set; } = BackpressureStrategy.Block;
}
