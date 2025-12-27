namespace BbQ.Cqrs;

/// <summary>
/// Marks a class as a query for source generation purposes.
/// When applied to a class, the source generator can generate handler registration
/// and other boilerplate code.
/// </summary>
/// <remarks>
/// This attribute is opt-in for source generation. Apply it to query classes
/// that you want the generator to process for automatic handler stub generation.
/// 
/// Usage:
/// <code>
/// [Query]
/// public class GetUserByIdQuery : IQuery&lt;Outcome&lt;User&gt;&gt;
/// {
///     public Guid UserId { get; set; }
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class QueryAttribute : Attribute
{
}
