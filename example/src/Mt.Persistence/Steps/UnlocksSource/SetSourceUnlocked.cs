using Mt.Domain.Migrations;
using Mt.Domain.Steps.UnlocksSource;
using Mt.Persistence.Migrations;
using Mt.Results;

namespace Mt.Persistence.Steps.UnlocksSource;

/// <summary>
/// Persists the <c>SourceUnlocked</c> fan-in flag and reports whether that finished the
/// teardown, and on which path (§6.3, spec 9).
/// </summary>
public sealed class SetSourceUnlocked(WorkshopDbContext db) : ISetSourceUnlocked
{
    public async Task<Result<ISetSourceUnlocked.Response>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var loaded = await db.LoadMigrationAsync(migrationId.Value, ct);
        return loaded.Map(row =>
        {
            row.SourceUnlocked = true;
            return row.ToDomain() switch
            {
                Unlocking { IsFullyUnlocked: true } unlocking => new ISetSourceUnlocked.Response.Complete(unlocking),
                Cancelling { IsFullyUnlocked: true } cancelling => new ISetSourceUnlocked.Response.Cancel(cancelling),
                _ => (ISetSourceUnlocked.Response)new ISetSourceUnlocked.Response.TeardownIncomplete(),
            };
        });
    }
}
