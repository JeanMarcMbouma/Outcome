namespace BbQ.Outcome
{
    /// <summary>
    /// Marks an enum for source code generation of Error helper properties.
    /// When applied to an enum, a source generator will create static Error properties
    /// for each enum value.
    /// </summary>
    [AttributeUsage(AttributeTargets.Enum)]
    public sealed class QbqOutcomeAttribute : Attribute
    {
    }
}
