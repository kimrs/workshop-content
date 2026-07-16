using Mt.Domain.Migrations;
using Mt.Domain.Steps.TriggersExport;
using Mt.Persistence.Migrations;
using Mt.Results;

namespace Mt.Persistence.Steps.TriggersExport;

/// <summary>
/// Persists the <c>ExportTriggered</c> fan-in flag; when it lands the last setup flag, the
/// same write performs <c>Created → Prepared</c> and reports it (§6.3, spec 9).
/// </summary>
public sealed class SetExportTriggered(WorkshopDbContext db) : ISetExportTriggered
{
    public async Task<Result<ISetExportTriggered.Response>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var loaded = await db.LoadMigrationAsync(migrationId.Value, ct);
        return loaded.Map(row =>
        {
            row.ExportTriggered = true;
            if (row.ToDomain() is not Created { IsReadyToTransform: true })
            {
                return (ISetExportTriggered.Response)new ISetExportTriggered.Response.SetupIncomplete();
            }

            row.State = MigrationState.Prepared;
            return new ISetExportTriggered.Response.SetupComplete();
        });
    }
}
