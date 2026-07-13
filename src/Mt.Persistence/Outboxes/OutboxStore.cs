using Microsoft.EntityFrameworkCore;
using Mt.Persistence.Rows;

namespace Mt.Persistence.Outboxes;

/// <summary>
/// Read/mark side of the outbox for the outbox worker (§8.2). Fetches pending rows, and marks
/// them processed or failed after the worker publishes them.
/// </summary>
public sealed class OutboxStore(WorkshopDbContext db)
{
    public async Task<IReadOnlyList<OutboxRow>> FetchPendingAsync(int batchSize, CancellationToken ct) =>
        await db.Outbox
            .Where(o => o.ProcessedAt == null && o.FailedAt == null)
            .OrderBy(o => o.Id)
            .Take(batchSize)
            .ToListAsync(ct);

    public async Task MarkProcessedAsync(long id, CancellationToken ct)
    {
        var row = await db.Outbox.FindAsync([id], ct);
        if (row is not null)
        {
            row.ProcessedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task MarkFailedAsync(long id, string reason, CancellationToken ct)
    {
        var row = await db.Outbox.FindAsync([id], ct);
        if (row is not null)
        {
            row.FailedAt = DateTimeOffset.UtcNow;
            row.FailureReason = reason;
            await db.SaveChangesAsync(ct);
        }
    }
}
