using System.Net;
using Mt.Results;

namespace Mt.Domain;

/// <summary>Locks the source and the target, then tells the user. The opening slide.</summary>
public sealed class Handler(
    ILockSource lockSource,
    ILockTarget lockTarget,
    INotifyCompletion notifyCompletion)
{
    public void Handle(Id migrationId)
    {
        var result = lockSource.Handle(migrationId)
            .Then(_ => lockTarget.Handle(migrationId))
            .Then(_ => notifyCompletion.Handle(new INotifyCompletion.Request.Migrated(migrationId)));

        if (result is Failed failed)
        {
            Console.WriteLine($"Failed {failed}");
        } else
        {
            Console.WriteLine($"Successfully locked source and target");
        }
    }
}
