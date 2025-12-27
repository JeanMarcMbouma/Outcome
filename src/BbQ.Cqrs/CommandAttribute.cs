namespace BbQ.Cqrs;

/// <summary>
/// Marks a class as a command for source generation purposes.
/// When applied to a class, the source generator can generate handler registration
/// and other boilerplate code.
/// </summary>
/// <remarks>
/// This attribute is opt-in for source generation. Apply it to command classes
/// that you want the generator to process for automatic handler stub generation.
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
