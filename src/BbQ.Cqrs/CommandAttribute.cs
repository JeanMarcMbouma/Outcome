namespace BbQ.Cqrs;

/// <summary>
/// Marks a class as a command so that it can be discovered by tooling and IDE support.
/// </summary>
/// <remarks>
/// This attribute is optional and primarily serves as metadata for tooling/IDE support.
/// The source generator automatically detects handlers for requests implementing ICommand&lt;T&gt;
/// without requiring this attribute.
/// 
/// Usage:
/// <code>
/// [Command]
/// public class CreateUserCommand : ICommand&lt;Outcome&lt;User&gt;&gt;
/// {
///     public string Email { get; set; }
///     public string Name { get; set; }
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CommandAttribute : Attribute
{
}
