using Mt.Domain;
using Mt.Results;

namespace Mt.Marble;

/// <summary>
/// Marble's completion notification. Reads the shared MigrationId straight off the base
/// request — no switch needed for shared data — and the switch has no discard: the
/// hierarchy is closed, so these two cases are all there can be.
/// </summary>
public sealed class NotifyCompletion : INotifyCompletion
{
    public Result<ValueTuple> Handle(INotifyCompletion.Request request)
    {
        var outcome = request switch
        {
            INotifyCompletion.Request.Migrated => "migrated to Marble",
            INotifyCompletion.Request.Cancelled => "cancelled",
        };

        Console.WriteLine($"📣 Marble hero hotline: migration {request.MigrationId.Value} {outcome}");
        return new Completed<ValueTuple>(default);
    }
}
