using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mt.Domain;
using Mt.Persistence.Rows;

namespace Mt.Persistence;

/// <summary>
/// Promotes due scheduled retries into the outbox (§11). The outbox worker calls this each poll
/// so a rescheduled attempt (attempt+1) becomes a real outbox message once its time has come.
/// </summary>
public sealed partial class Scheduler(WorkshopDbContext db, ILogger<Scheduler> logger)
{
    public async Task<int> PromoteDueAsync(int batchSize, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var due = await db.ScheduledEvents
            .Where(s => s.ProcessedAt == null && s.ScheduledAt <= now)
            .OrderBy(s => s.ScheduledAt)
            .Take(batchSize)
            .ToListAsync(ct);

        foreach (var scheduled in due)
        {
            db.Outbox.Add(new OutboxRow
            {
                MigrationId = scheduled.MigrationId,
                DomainEvent = scheduled.DomainEvent,
                Attempt = scheduled.Attempt,
                Payload = scheduled.Payload,
                // Carry the originating trace: Activity.Current is always null in this poll loop (spec 12 D8).
                TraceParent = scheduled.TraceParent,
                CreatedAt = now,
            });
            scheduled.ProcessedAt = now;
        }

        if (due.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            Log.RetriesPromoted(logger, due.Count);
        }

        return due.Count;
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = LogEvents.RetriesPromoted, EventName = nameof(LogEvents.RetriesPromoted),
            Level = LogLevel.Information, Message = "Promoted {Count} scheduled retries into the outbox.")]
        public static partial void RetriesPromoted(ILogger logger, int count);
    }
}
