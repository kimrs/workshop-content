using Microsoft.EntityFrameworkCore;
using Mt.Persistence.Rows;
using Mt.Results;

namespace Mt.Persistence.Migrations;

/// <summary>
/// Shared "load the tracked migration row or fail with NotFound" used by every persistence
/// port. This is legitimate reuse in the adapter layer past the fourth repetition (§4.3) —
/// it is not one of the domain stage slices the spec keeps duplicated.
/// </summary>
internal static class MigrationRowLookup
{
    extension(WorkshopDbContext db)
    {
        public async Task<Result<MigrationRow>> LoadMigrationAsync(long migrationId, CancellationToken ct)
        {
            var row = await db.Migrations.FirstOrDefaultAsync(m => m.Id == migrationId, ct).ConfigureAwait(false);
            return row.EnsureFound($"Migration {migrationId} not found.");
        }
    }
}
