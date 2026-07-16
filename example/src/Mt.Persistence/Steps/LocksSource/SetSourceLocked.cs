using Mt.Domain.Migrations;
using Mt.Domain.Steps.LocksSource;
using Mt.Persistence.Migrations;
using Mt.Results;

namespace Mt.Persistence.Steps.LocksSource;

/// <summary>
/// Persists the <c>SourceLocked</c> fan-in flag; when it lands the last setup flag, the same
/// write performs <c>Created → Prepared</c> and reports it (§6.3, spec 9).
/// </summary>
public sealed class SetSourceLocked(WorkshopDbContext db) : ISetSourceLocked
{
    public async Task<Result<ISetSourceLocked.Response>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var loaded = await db.LoadMigrationAsync(migrationId.Value, ct);
        return loaded.Map(row =>
        {
            row.SourceLocked = true;
            if (row.ToDomain() is not Created { IsReadyToTransform: true })
            {
                return (ISetSourceLocked.Response)new ISetSourceLocked.Response.SetupIncomplete();
            }

            row.State = MigrationState.Prepared;
            return new ISetSourceLocked.Response.SetupComplete();
        });
    }
}
