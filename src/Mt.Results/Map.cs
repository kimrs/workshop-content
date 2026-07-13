namespace Mt.Results;

/// <summary>Transform a success value, leaving failures untouched (§5.2).</summary>
public static class MapExtensions
{
    extension<T>(Result<T> result)
    {
        public Result<TRes> Map<TRes>(Func<T, TRes> transform) =>
            result is Result<T>.Completed completed
                ? new Result<TRes>.Completed(transform(completed.Value))
                : new Result<TRes>.Failed(((Result<T>.Failed)result).Failures);
    }
}
