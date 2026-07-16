namespace Mt.Results;

/// <summary>
/// Minimal fluent validation: fail a value when a predicate holds, otherwise carry
/// it forward as a completed result. Guards chain on the in-flight result so a
/// <c>Create</c> reads as <c>line.FailWhen(...).FailWhen(...).Then(Create)</c>.
/// Only the <c>FailWhen</c> form is supported — the legacy <c>EmptyWhen*</c>
/// variants are excluded (§5.2, §10).
/// </summary>
public static class FailWhenExtensions
{
    extension<T>(T value)
    {
        public Result<T> FailWhen(Func<T, bool> predicate, string message) =>
            predicate(value)
                ? new ValidationFailure(message)
                : value;
    }

    extension<T>(Result<T> result)
    {
        public Result<T> FailWhen(Func<T, bool> predicate, string message) =>
            result is Result<T>.Completed completed && predicate(completed.Value)
                ? new Result<T>.Failed([new ValidationFailure(message)])
                : result;
    }
}
