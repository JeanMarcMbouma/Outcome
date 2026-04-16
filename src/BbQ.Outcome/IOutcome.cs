
namespace BbQ.Outcome
{
    /// <summary>
    /// Core interface for a discriminated union result type with strongly-typed errors.
    /// </summary>
    /// <typeparam name="T">The type of the successful value.</typeparam>
    /// <typeparam name="TError">The type of each error in the error list.</typeparam>
    public interface IOutcome<T, TError>
    {
        /// <summary>Gets the strongly-typed list of errors.</summary>
        IReadOnlyList<TError> Errors { get; }

        /// <summary>Gets whether this outcome represents a failed operation.</summary>
        bool IsError { get; }

        /// <summary>Gets whether this outcome represents a successful operation.</summary>
        bool IsSuccess { get; }

        /// <summary>Gets the successful value.</summary>
        T Value { get; }

        /// <summary>Deconstructs the outcome into success flag, value, and errors.</summary>
        void Deconstruct(out bool isSuccess, out T? value, out IReadOnlyList<TError>? errors);

        /// <summary>Deconstructs the outcome into value and errors.</summary>
        void Deconstruct(out T? value, out IReadOnlyList<TError>? errors);

        /// <summary>Returns a human-readable string representation.</summary>
        string ToString();
    }

    /// <summary>
    /// Interface for a discriminated union result type with heterogeneous (<c>object?</c>) errors.
    /// Extends <see cref="IOutcome{T, TError}"/> with <c>TError = object?</c>.
    /// </summary>
    /// <typeparam name="T">The type of the successful value.</typeparam>
    public interface IOutcome<T> : IOutcome<T, object?>
    {
        static abstract Outcome<T> From(T value);
        static abstract Outcome<T> FromErrors(IReadOnlyList<object> errors);
    }
}