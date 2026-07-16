namespace Mt.Results;

/// <summary>
/// <c>Then</c> chains fallible steps: on a completed result it runs the next step,
/// on a failed result it propagates the failures (§4.4, §5.2). Async producers
/// return <see cref="AsyncResult{T}"/> and use <c>ConfigureAwait(false)</c>.
/// </summary>
public static class ThenExtensions
{
    extension<T>(Result<T> result)
    {
        public Result<TRes> Then<TRes>(Func<T, TRes> next) =>
            result is Result<T>.Completed completed
                ? new Result<TRes>.Completed(next(completed.Value))
                : Fail<T, TRes>(result);

        public Result<TRes> Then<TRes>(Func<T, Result<TRes>> next) =>
            result is Result<T>.Completed completed
                ? next(completed.Value)
                : Fail<T, TRes>(result);

        public AsyncResult<TRes> ThenAsync<TRes>(Func<T, Task<Result<TRes>>> next) =>
            ThenAsyncCore(result, next);
    }

    extension<T>(AsyncResult<T> asyncResult)
    {
        public AsyncResult<TRes> ThenAsync<TRes>(Func<T, Task<Result<TRes>>> next) =>
            ChainAsync(asyncResult, r => r.ThenAsync(next).AsTask());

        public AsyncResult<TRes> Then<TRes>(Func<T, Result<TRes>> next) =>
            ChainAsync(asyncResult, r => Task.FromResult(r.Then(next)));
    }

    private static Result<TRes> Fail<T, TRes>(Result<T> result) =>
        new Result<TRes>.Failed(((Result<T>.Failed)result).Failures);

    private static async Task<Result<TRes>> ThenAsyncCore<T, TRes>(
        Result<T> result,
        Func<T, Task<Result<TRes>>> next)
    {
        return result is Result<T>.Completed completed
            ? await next(completed.Value).ConfigureAwait(false)
            : Fail<T, TRes>(result);
    }

    private static async Task<Result<TRes>> ChainAsync<T, TRes>(
        AsyncResult<T> asyncResult,
        Func<Result<T>, Task<Result<TRes>>> next)
    {
        var result = await asyncResult.AsTask().ConfigureAwait(false);
        return await next(result).ConfigureAwait(false);
    }
}
