namespace Mt.Results;

/// <summary>
/// <c>Then</c> chains fallible steps: on a completed result it runs the next step, on a
/// failed result it propagates the reason. No discard arm — the union's case list is the
/// whole story.
/// </summary>
public static class ThenExtensions
{
    extension<T>(Result<T> result)
    {
        public Result<TRes> Then<TRes>(Func<T, TRes> next) =>
            result switch
            {
                Completed<T>(var value) => new Completed<TRes>(next(value)),
                Failed f => f
            };

        public Result<TRes> Then<TRes>(Func<T, Result<TRes>> next) =>
            result switch
            {
                Completed<T>(var value) => next(value),
                Failed f => f
            };
    }
}
