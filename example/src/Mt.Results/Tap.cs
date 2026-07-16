namespace Mt.Results;

/// <summary>
/// Run a side effect on success and return the result unchanged. This is the only
/// side-effect combinator — do not add <c>Do</c> (§5.2, §10).
/// </summary>
public static class TapExtensions
{
    extension<T>(Result<T> result)
    {
        public Result<T> Tap(Action<T> effect)
        {
            if (result is Result<T>.Completed completed)
            {
                effect(completed.Value);
            }

            return result;
        }
    }
}
