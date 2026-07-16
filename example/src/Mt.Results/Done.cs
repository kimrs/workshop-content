namespace Mt.Results;

/// <summary>
/// The completed unit result: handled, nothing further to do (spec 11). <see cref="Task"/>
/// is the cached task of <see cref="Result"/> for async fold arms.
/// </summary>
public static class Done
{
    public static Result<ValueTuple> Result { get; } = new Result<ValueTuple>.Completed(default);

    public static Task<Result<ValueTuple>> Task { get; } = System.Threading.Tasks.Task.FromResult(Result);
}
