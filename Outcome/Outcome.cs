using System.Runtime.CompilerServices;

namespace Outcome
{
    public readonly struct Outcome<T>
    {
        private readonly T? _value;
        private readonly IReadOnlyList<object?> _errors;
        public bool IsSuccess { get; }
        public bool IsError => !IsSuccess;
        public T Value
        {
            get
            {
                if (!IsSuccess)
                    throw new InvalidOperationException("Cannot access Value when Outcome is a failure.");
                return _value!;
            }
        }

        public IReadOnlyList<object?> Errors
        {
            get
            {
                if (IsSuccess)
                    throw new InvalidOperationException("Cannot access Errors when Outcome is a success.");
                return _errors;
            }
        }

        private Outcome(T value) => (_value, IsSuccess, _errors) = (value, true, []);
        private Outcome(IReadOnlyList<object?> errors) => (_errors, _value, IsSuccess) = (errors, default, false);

        public static Outcome<T> From(T value) => new (value);
        public static Outcome<T> FromErrors(IReadOnlyList<object> errors) => new (errors);

        public static implicit operator Outcome<T>(T value) => new (value);

        public TResult Match<TResult>(
            Func<T, TResult> onSuccess,
            Func<IReadOnlyList<object?>, TResult> onError)
        {
            return IsSuccess
                ? onSuccess(Value)
                : onError(Errors);
        }

        public void Switch(
            Action<T> onSuccess,
            Action<IReadOnlyList<object?>> onError)
        {
            if (IsSuccess)
                onSuccess(Value);
            else
                onError(Errors);
        }

        public Outcome<TResult> Bind<TResult>(Func<T, Outcome<TResult>> binder)
        {
            return IsSuccess
                ? binder(Value)
                : Outcome<TResult>.FromErrors(Errors!);
        }

        public Outcome<TResult> Map<TResult>(Func<T, TResult> mapper)
        {
            return IsSuccess
                ? Outcome<TResult>.From(mapper(Value))
                : Outcome<TResult>.FromErrors(Errors!);
        }

        public static Outcome<IEnumerable<T>> Combine(params IEnumerable<Outcome<T>> outcomes)
        {
            var errors = outcomes
                .Where(o => o.IsError)
                .SelectMany(o => o.Errors);
            return errors.Any()
                ? Outcome<IEnumerable<T>>.FromErrors(errors.ToList()!)
                : Outcome<IEnumerable<T>>.From(outcomes.Select(r => r.Value));
        }
        public override string ToString()
        {
            return IsSuccess ? $"Success: {Value}" : $"Error: [{string.Join(", ", Errors)}]";
        }

        public void Deconstruct(out bool isSuccess, out T? value, out IReadOnlyList<object?>? errors)
        {
            isSuccess = IsSuccess;
            value = IsSuccess ? _value : default;
            errors = IsSuccess ? null : _errors;
        }
    }

    public enum ErrorSeverity
    {
        Info,
        Validation,
        Warning,
        Error,
        Critical
    }
    public sealed record Error<TCode>(TCode Code, string Description, ErrorSeverity Severity = ErrorSeverity.Error);

    public static class ErrorExtensions
    {
        extension<TCode>(Error<TCode>)
        {
            public static Error<TCode> Info(TCode code, string description)
            {
                return new Error<TCode>(code, description, ErrorSeverity.Info);
            }
            public static Error<TCode> Validation(TCode code, string description)
            {
                return new Error<TCode>(code, description, ErrorSeverity.Validation);
            }
            public static Error<TCode> Warning(TCode code, string description)
            {
                return new Error<TCode>(code, description, ErrorSeverity.Warning);
            }

            public static Error<TCode> Critical(TCode code, string description)
            {
                return new Error<TCode>(code, description, ErrorSeverity.Critical);
            }
        }

    }

    public static class OutcomeExtensions
    {
        extension<T>(Outcome<T>)
        {
            public static Outcome<T> FromError<TCode>(TCode code, string description, ErrorSeverity severity = ErrorSeverity.Error)
            {
                var error = new Error<TCode>(code, description, severity);
                return Outcome<T>.FromErrors([error]);
            }

            public static Outcome<T> FromError<TCode>(Error<TCode> error)
            {
                return Outcome<T>.FromErrors([error]);
            }
            public static Outcome<T> FromErrors<TCode>(IEnumerable<Error<TCode>> errors)
            {
                return Outcome<T>.FromErrors(errors.ToList());
            }
            public static Outcome<T> Validation<TCode>(TCode code, string description)
            {
                var error = new Error<TCode>(code, description, ErrorSeverity.Validation);
                return Outcome<T>.FromErrors([error]);
            }
            public static Outcome<T> Critical<TCode>(TCode code, string description)
            {
                var error = new Error<TCode>(code, description, ErrorSeverity.Critical);
                return Outcome<T>.FromErrors([error]);
            }
        }

    }

    public static class  OutcomeLinqExtensions
    {
        extension<T>(Outcome<T> outcome)
        {
            public Outcome<TResult> Select<TResult>(Func<T, TResult> selector)
                => outcome.IsSuccess
                    ? Outcome<TResult>.From(selector(outcome.Value))
                    : Outcome<TResult>.FromErrors(outcome.Errors!);
            public Outcome<TResult> SelectMany<TIntermediate, TResult>( 
                Func<T,Outcome<TIntermediate>> binder,
                Func<T, TIntermediate, TResult> projector)
                => outcome.IsSuccess
                    ? binder(outcome.Value).Select(intermediate => projector(outcome.Value, intermediate))
                    : Outcome<TResult>.FromErrors(outcome.Errors!);
        }


        extension<T>(Task<Outcome<T>> task)
        {
            public async Task<Outcome<TResult>> Select<TResult>(Func<T, TResult> selector)
            {
                var outcome = await task.ConfigureAwait(false);
                return outcome.IsSuccess
                    ? Outcome<TResult>.From(selector(outcome.Value))
                    : Outcome<TResult>.FromErrors(outcome.Errors!);
            }

            public async Task<Outcome<TResult>> SelectMany<TIntermediate, TResult>(
                Func<T, Outcome<TIntermediate>> binder,
                Func<T, TIntermediate, TResult> projector)
            {
                var outcome = await task.ConfigureAwait(false);
                return outcome.IsSuccess
                    ? binder(outcome.Value).Select(intermediate => projector(outcome.Value, intermediate))
                    : Outcome<TResult>.FromErrors(outcome.Errors!);
            }
        }
    }
}
