using Microsoft.EntityFrameworkCore;
using Mt.Domain.ExternalIds;
using Mt.Results;

namespace Mt.Persistence.ExternalIds;

/// <summary>
/// Resolves what one external system calls a migration (spec 8). Active rows only; a
/// missing row is <see cref="NoExternalIdFailure"/>, a corrupt one a rehydration failure.
/// </summary>
public sealed class Fetch(WorkshopDbContext db) : IFetch
{
    public async Task<Result<ExternalId>> HandleAsync(IFetch.Request request, CancellationToken ct)
    {
        var row = await db.ExternalIds.FirstOrDefaultAsync(
            e => e.MigrationId == request.MigrationId.Value
                && e.System == request.System.Value
                && e.Name == request.Type.Value
                && !e.IsCancelled,
            ct);

        if (row is null)
        {
            return new NoExternalIdFailure(
                $"No active {request.System.Value} {request.Type.Value} for migration {request.MigrationId.Value}.");
        }

        return row.ToDomain();
    }
}
