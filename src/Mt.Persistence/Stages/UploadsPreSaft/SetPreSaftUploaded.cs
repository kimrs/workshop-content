using Mt.Domain.Migrations;
using Mt.Domain.Stages.UploadsPreSaft;
using Mt.Persistence.Migrations;
using Mt.Results;

namespace Mt.Persistence.Stages.UploadsPreSaft;

/// <summary>Persists the <c>Transformed → PreSaftUploaded</c> transition (§6.5).</summary>
public sealed class SetPreSaftUploaded(WorkshopDbContext db) : ISetPreSaftUploaded
{
    public async Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var loaded = await db.LoadMigrationAsync(migrationId.Value, ct);
        return loaded
            .Tap(row => row.State = MigrationState.PreSaftUploaded)
            .Map(_ => default(ValueTuple));
    }
}
