namespace Mt.Results;

/// <summary>
/// Fold a result into a plain value: one continuation per case (spec 11). For boundaries
/// that translate a result into something else — inside a flow, chain with <c>Then</c> instead.
/// </summary>
public static class MatchExtensions
{
    extension<T>(Result<T> result)
    {
        public TRes Match<TRes>(Func<T, TRes> completed, Func<IReadOnlyList<Failure>, TRes> failed) =>
            result is Result<T>.Completed c
                ? completed(c.Value)
                : failed(((Result<T>.Failed)result).Failures);
    }
}
