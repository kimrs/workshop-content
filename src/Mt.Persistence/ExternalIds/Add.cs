using Mt.Domain.ExternalIds;
using Mt.Results;

namespace Mt.Persistence.ExternalIds;

/// <summary>
/// Records an external id in the caller's unit of work — no <c>SaveChanges</c>; the surrounding
/// transaction commits (spec 8). Idempotent on the PK: same value → no-op, different value →
/// <see cref="ConflictingExternalIdFailure"/>.
/// </summary>
public sealed class Add(WorkshopDbContext db) : IAdd
{
    public async Task<Result<ValueTuple>> HandleAsync(IAdd.Request request, CancellationToken ct)
    {
        var existing = await db.ExternalIds.FindAsync(
            [request.MigrationId.Value, request.System.Value, request.Type.Value], ct);

        if (existing is null)
        {
            db.ExternalIds.Add(new Rows.ExternalIdRow
            {
                MigrationId = request.MigrationId.Value,
                System = request.System.Value,
                Name = request.Type.Value,
                Value = request.Value.Value,
            });
            return default(ValueTuple);
        }

        if (existing.Value == request.Value.Value)
        {
            return default(ValueTuple);
        }

        return new ConflictingExternalIdFailure(
            $"{request.System.Value} {request.Type.Value} for migration {request.MigrationId.Value} "
            + $"is already recorded as '{existing.Value}' but '{request.Value.Value}' was supplied.");
    }
}
