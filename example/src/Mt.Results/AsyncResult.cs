using System.Runtime.CompilerServices;

namespace Mt.Results;

/// <summary>
/// An awaitable wrapper over <see cref="Task{TResult}"/> of <see cref="Result{T}"/>
/// so async chains read fluently and can be <c>await</c>ed directly (§5.2).
/// </summary>
public readonly struct AsyncResult<T>(Task<Result<T>> task)
{
    private readonly Task<Result<T>> _task = task;

    public Task<Result<T>> AsTask() => _task;

    public TaskAwaiter<Result<T>> GetAwaiter() => _task.GetAwaiter();

    public static implicit operator AsyncResult<T>(Task<Result<T>> task) => new(task);
}
