using Mt.Domain;
using Mt.Results;

namespace Mt.Marble;

/// <summary>
/// Marble's connector for their own system: a well-behaved vendor that answers only with
/// the responses the port declares.
/// </summary>
public sealed class LockTarget : ILockTarget
{
    public Result<ILockTarget.Response> Handle(Id migrationId) =>
        new Completed<ILockTarget.Response>(new ILockTarget.Response.Locked());
}
