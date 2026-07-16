using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Persistence.Migrations.Sets;

/// <summary>Persists the terminal <c>Cancelled</c> state (§6.3).</summary>
public sealed class SetCancelled(WorkshopDbContext db) : ISetCancelled
{
    public async Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var loaded = await db.LoadMigrationAsync(migrationId.Value, ct);
        return loaded
            .Tap(row => row.State = MigrationState.Cancelled)
            .Map(_ => default(ValueTuple));
    }
}
