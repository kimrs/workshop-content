using Mt.Domain.Migrations;
using Mt.Domain.Steps.LocksTarget;
using Mt.Persistence.Migrations;
using Mt.Results;

namespace Mt.Persistence.Steps.LocksTarget;

/// <summary>
/// Persists the <c>TargetLocked</c> fan-in flag; when it lands the last setup flag, the same
/// write performs <c>Created → Prepared</c> and reports it (§6.3, spec 9).
/// </summary>
public sealed class SetTargetLocked(WorkshopDbContext db) : ISetTargetLocked
{
    public async Task<Result<ISetTargetLocked.Response>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var loaded = await db.LoadMigrationAsync(migrationId.Value, ct);
        return loaded.Map(ISetTargetLocked.Response (row) =>
        {
            row.TargetLocked = true;
            if (row.ToDomain() is not Created { IsReadyToTransform: true })
            {
                return new ISetTargetLocked.Response.SetupIncomplete();
            }

            row.State = MigrationState.Prepared;
            return new ISetTargetLocked.Response.SetupComplete();
        });
    }
}
