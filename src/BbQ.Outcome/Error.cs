using System.Diagnostics.CodeAnalysis;

namespace BbQ.Outcome
{
    /// <summary>
    /// Represents a structured error with a code, description, and severity level.
    /// </summary>
    /// <typeparam name="TCode">The type of the error code (e.g., enum, string, int).</typeparam>
    /// <param name="Code">The error code used to identify the error type.</param>
    /// <param name="Description">A human-readable description of what went wrong.</param>
    /// <param name="Severity">
    /// The severity level of this error. Defaults to <see cref="ErrorSeverity.Error"/>.
    /// </param>
    public record Error<TCode>(TCode Code, string Description, ErrorSeverity Severity = ErrorSeverity.Error)
    {
        /// <summary>
        /// Creates an error with a formatted description from an interpolated string.
        /// Only accepts interpolated strings (e.g. <c>$"User {id} not found"</c>), not plain strings.
        /// </summary>
        /// <example>
        /// <code>
        /// var error = Error&lt;AppError&gt;.Create(AppError.UserNotFound, $"User {userId} not found");
        /// </code>
        /// </example>
        /// <param name="code">The error code used to identify the error type.</param>
        /// <param name="description">An interpolated string describing what went wrong.</param>
        /// <returns>A new <see cref="Error{TCode}"/> with the formatted description.</returns>
        public static Error<TCode> Create(TCode code, FormattableString description)
            => new(code, description.ToString());

        /// <summary>
        /// Creates an error with a formatted description and explicit severity from an interpolated string.
        /// Only accepts interpolated strings (e.g. <c>$"User {id} not found"</c>), not plain strings.
        /// </summary>
        /// <example>
        /// <code>
        /// var error = Error&lt;AppError&gt;.Create(AppError.UserNotFound, ErrorSeverity.Critical, $"User {userId} not found");
        /// </code>
        /// </example>
        /// <param name="code">The error code used to identify the error type.</param>
        /// <param name="severity">The severity level of this error.</param>
        /// <param name="description">An interpolated string describing what went wrong.</param>
        /// <returns>A new <see cref="Error{TCode}"/> with the formatted description and specified severity.</returns>
        public static Error<TCode> Create(TCode code, ErrorSeverity severity, FormattableString description)
            => new(code, description.ToString(), severity);

        /// <summary>
        /// Creates an error with a message template and arguments, similar to <c>ILogger</c>.
        /// Named placeholders are replaced positionally (e.g. <c>"User {UserId} not found"</c>).
        /// </summary>
        /// <example>
        /// <code>
        /// var error = Error&lt;AppError&gt;.Create(AppError.UserNotFound, "User {UserId} not found", userId);
        /// </code>
        /// </example>
        /// <param name="code">The error code used to identify the error type.</param>
        /// <param name="template">A message template with named placeholders (e.g. <c>{Name}</c>).</param>
        /// <param name="args">The values to substitute into the template placeholders, in order.</param>
        /// <returns>A new <see cref="Error{TCode}"/> with the formatted description.</returns>
        public static Error<TCode> Create(
            TCode code,
            [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string template,
            params object?[] args)
            => new(code, ErrorMessageFormatter.Format(template, args));

        /// <summary>
        /// Creates an error with an explicit severity using a message template and arguments, similar to <c>ILogger</c>.
        /// Named placeholders are replaced positionally (e.g. <c>"User {UserId} not found"</c>).
        /// </summary>
        /// <example>
        /// <code>
        /// var error = Error&lt;AppError&gt;.Create(AppError.UserNotFound, ErrorSeverity.Critical, "User {UserId} not found", userId);
        /// </code>
        /// </example>
        /// <param name="code">The error code used to identify the error type.</param>
        /// <param name="severity">The severity level of this error.</param>
        /// <param name="template">A message template with named placeholders (e.g. <c>{Name}</c>).</param>
        /// <param name="args">The values to substitute into the template placeholders, in order.</param>
        /// <returns>A new <see cref="Error{TCode}"/> with the formatted description and specified severity.</returns>
        public static Error<TCode> Create(
            TCode code,
            ErrorSeverity severity,
            [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string template,
            params object?[] args)
            => new(code, ErrorMessageFormatter.Format(template, args), severity);
    }
}
