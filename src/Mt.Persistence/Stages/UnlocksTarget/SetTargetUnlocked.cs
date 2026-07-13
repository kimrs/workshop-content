using Mt.Domain.Migrations;
using Mt.Domain.Stages.UnlocksTarget;
using Mt.Persistence.Migrations;
using Mt.Results;

namespace Mt.Persistence.Stages.UnlocksTarget;

/// <summary>
/// Persists the <c>TargetUnlocked</c> fan-in flag and reports whether that finished the
/// teardown, and on which path (§6.3, spec 9).
/// </summary>
public sealed class SetTargetUnlocked(WorkshopDbContext db) : ISetTargetUnlocked
{
    public async Task<Result<ISetTargetUnlocked.Response>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var loaded = await db.LoadMigrationAsync(migrationId.Value, ct);
        return loaded.Map(row =>
        {
            row.TargetUnlocked = true;
            return row.ToDomain() switch
            {
                Unlocking { IsFullyUnlocked: true } unlocking => new ISetTargetUnlocked.Response.Complete(unlocking),
                Cancelling { IsFullyUnlocked: true } cancelling => new ISetTargetUnlocked.Response.Cancel(cancelling),
                _ => (ISetTargetUnlocked.Response)new ISetTargetUnlocked.Response.TeardownIncomplete(),
            };
        });
    }
}
