using Microsoft.EntityFrameworkCore;
using Mt.Domain.ExternalIds;
using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Persistence.ExternalIds;

/// <summary>
/// Releases every external id of a migration (spec 8): bulk soft-delete inside the ambient
/// transaction. Deliberately no <c>IsCancelled</c> filter — re-cancelling is a no-op.
/// </summary>
public sealed class Cancel(WorkshopDbContext db) : ICancel
{
    public async Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        await db.ExternalIds
            .Where(e => e.MigrationId == migrationId.Value)
            .ExecuteUpdateAsync(set => set.SetProperty(e => e.IsCancelled, true), ct);

        return default(ValueTuple);
    }
}
