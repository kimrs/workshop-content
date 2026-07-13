using Mt.Domain.Migrations;
using Mt.Domain.Stages.UploadsSaft;
using Mt.Persistence.Migrations;
using Mt.Results;

namespace Mt.Persistence.Stages.UploadsSaft;

/// <summary>Persists the <c>PreSaftUploaded → SaftUploaded</c> transition (§6.5).</summary>
public sealed class SetSaftUploaded(WorkshopDbContext db) : ISetSaftUploaded
{
    public async Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var loaded = await db.LoadMigrationAsync(migrationId.Value, ct);
        return loaded
            .Tap(row => row.State = MigrationState.SaftUploaded)
            .Map(_ => default(ValueTuple));
    }
}
