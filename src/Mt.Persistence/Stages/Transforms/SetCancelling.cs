using Mt.Domain.Migrations;
using Mt.Domain.Stages.Transforms;
using Mt.Persistence.Migrations;
using Mt.Results;

namespace Mt.Persistence.Stages.Transforms;

/// <summary>
/// Persists the automatic <c>Created → Cancelling</c> transition (§6.5). Reached only when
/// both systems are already locked, so both unlock flags are seeded to <c>false</c>.
/// </summary>
public sealed class SetCancelling(WorkshopDbContext db) : ISetCancelling
{
    public async Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var loaded = await db.LoadMigrationAsync(migrationId.Value, ct);
        return loaded
            .Tap(row =>
            {
                row.State = MigrationState.Cancelling;
                row.SourceUnlocked = false;
                row.TargetUnlocked = false;
            })
            .Map(_ => default(ValueTuple));
    }
}
