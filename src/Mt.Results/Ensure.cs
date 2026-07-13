namespace Mt.Results;

/// <summary>
/// Small guards that turn missing or duplicated values into typed failures (§5.2).
/// </summary>
public static class Ensure
{
    extension<T>(T? value)
        where T : class
    {
        public Result<T> EnsureNotNull(string message) =>
            value is null
                ? new UnexpectedNullFailure(message)
                : value;

        public Result<T> EnsureFound(string message) =>
            value is null
                ? new NotFoundFailure(message)
                : value;
    }

    extension<T>(IEnumerable<T> items)
    {
        public Result<IReadOnlyList<T>> EnsureNoDuplicates(string message)
        {
            var materialized = items.ToArray();
            return materialized.Length != materialized.Distinct().Count()
                ? new DuplicateFailure(message)
                : new Result<IReadOnlyList<T>>.Completed(materialized);
        }
    }
}
