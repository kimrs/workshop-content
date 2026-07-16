using Mt.Domain.Migrations;
using Mt.Domain.Steps.Transforms;
using Mt.Persistence.Migrations;
using Mt.Results;

namespace Mt.Persistence.Steps.Transforms;

/// <summary>Persists the <c>Created → Transformed</c> transition (§6.5).</summary>
public sealed class SetTransformed(WorkshopDbContext db) : ISetTransformed
{
    public async Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var loaded = await db.LoadMigrationAsync(migrationId.Value, ct);
        return loaded
            .Tap(row => row.State = MigrationState.Transformed)
            .Map(_ => default(ValueTuple));
    }
}
