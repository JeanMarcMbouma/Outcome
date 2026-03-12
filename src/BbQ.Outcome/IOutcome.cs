
namespace BbQ.Outcome
{
    public interface IOutcome<T>
    {
        IReadOnlyList<object?> Errors { get; }
        bool IsError { get; }
        bool IsSuccess { get; }
        T Value { get; }

        static abstract Outcome<T> From(T value);
        static abstract Outcome<T> FromErrors(IReadOnlyList<object> errors);
        void Deconstruct(out bool isSuccess, out T? value, out IReadOnlyList<object?>? errors);
        void Deconstruct(out T? value, out IReadOnlyList<object?>? errors);
        string ToString();
    }
}