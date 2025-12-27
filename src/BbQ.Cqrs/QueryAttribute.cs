namespace BbQ.Cqrs;

/// <summary>
/// Marks a class as a query so that it can be discovered by tooling and IDE support.
/// </summary>
/// <remarks>
/// This attribute is optional and primarily serves as metadata for tooling/IDE support.
/// The source generator automatically detects handlers for requests implementing IQuery&lt;T&gt;
/// without requiring this attribute.
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
