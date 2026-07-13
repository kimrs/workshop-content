using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Persistence.Migrations.Sets;

/// <summary>Persists the terminal <c>Completed</c> state (§6.3).</summary>
public sealed class SetCompleted(WorkshopDbContext db) : ISetCompleted
{
    public async Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var loaded = await db.LoadMigrationAsync(migrationId.Value, ct);
        return loaded
            .Tap(row => row.State = MigrationState.Completed)
            .Map(_ => default(ValueTuple));
    }
}
