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

    /// <summary>
    /// Marks an enum member with a custom severity level for error generation.
    /// When not specified, the default severity is <see cref="ErrorSeverity.Error"/>.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ErrorSeverityAttribute"/> class.
    /// </remarks>
    /// <param name="severity">The severity level for this error.</param>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ErrorSeverityAttribute(ErrorSeverity severity) : Attribute
    {
        /// <summary>
        /// Gets the severity level for this error.
        /// </summary>
        public ErrorSeverity Severity { get; } = severity;
    }
}
