namespace BbQ.Cqrs;

/// <summary>
/// Marks a pipeline behavior for automatic registration with a specific execution order.
/// Only behaviors with this attribute are automatically registered by the source generator.
/// </summary>
/// <remarks>
/// The Order property determines the execution order in the pipeline:
/// - Lower values execute first (outermost in the pipeline)
/// - Higher values execute later (closer to the handler)
/// - Default value is 0 if not specified
/// 
/// <strong>Important:</strong> The behavior class must have exactly 2 type parameters matching
/// IPipelineBehavior&lt;TRequest, TResponse&gt;. Behaviors with additional type parameters
/// (e.g., 3 or more) cannot be automatically registered and must be registered manually.
/// 
/// Usage:
/// <code>
/// [Behavior(Order = 1)]
/// public class LoggingBehavior&lt;TRequest, TResponse&gt; : IPipelineBehavior&lt;TRequest, TResponse&gt;
///     where TRequest : IRequest&lt;TResponse&gt;
/// {
///     // Implementation...
/// }
/// 
/// [Behavior(Order = 2)]
/// public class ValidationBehavior&lt;TRequest, TResponse&gt; : IPipelineBehavior&lt;TRequest, TResponse&gt;
///     where TRequest : IRequest&lt;TResponse&gt;
/// {
///     // Implementation...
/// }
/// </code>
/// 
/// In this example:
/// - LoggingBehavior (Order = 1) executes first (outermost)
/// - ValidationBehavior (Order = 2) executes second
/// - Then the handler executes
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class BehaviorAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the execution order of this behavior in the pipeline.
    /// Lower values execute first (outermost). Default is 0.
    /// </summary>
    public int Order { get; set; } = 0;
}
