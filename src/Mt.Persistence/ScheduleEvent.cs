using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Mt.Domain;
using Mt.Domain.Migrations;
using Mt.Domain.Stages;
using Mt.Persistence.Rows;
using Mt.Results;

namespace Mt.Persistence;

/// <summary>
/// Owns the retry arithmetic (spec 7): the current attempt is <c>MAX(Attempt)</c> in the inbox
/// for <c>(MigrationId, DomainEvent)</c> — the in-flight message's claim row is already flushed
/// in the ambient transaction, so that is exactly the attempt being processed. Below the caller's
/// budget it records the next attempt as a <see cref="ScheduledEventRow"/> due after a short
/// back-off (§11); at or past it, it schedules nothing and reports <c>Exhausted</c>.
/// No <c>SaveChanges</c> — it commits with the inbox transaction, so "handled + retry scheduled"
/// is atomic.
/// </summary>
public sealed class ScheduleEvent(WorkshopDbContext db) : IScheduleEvent
{
    // Short, workshop-friendly back-off so retries are visible but not sluggish.
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public async Task<Result<IScheduleEvent.Response>> HandleAsync(
        Id migrationId, DomainEvent domainEvent, int maxAttempts, CancellationToken ct)
    {
        var eventName = domainEvent.ToMessageType();
        var currentOrNone = await db.Inbox
            .Where(row => row.MigrationId == migrationId.Value && row.DomainEvent == eventName)
            .MaxAsync(row => (int?)row.Attempt, ct);
        if (currentOrNone is not int current)
        {
            // The contract, made diagnosable (spec 12 D9): the caller's inbox claim must
            // already be flushed in the ambient transaction.
            return new NotFoundFailure(
                $"No inbox claim for {eventName} on migration {migrationId.Value} — "
                + "IScheduleEvent must run inside the inbox transaction.");
        }

        if (current >= maxAttempts)
        {
            return new IScheduleEvent.Response.Exhausted();
        }

        db.ScheduledEvents.Add(new ScheduledEventRow
        {
            MigrationId = migrationId.Value,
            DomainEvent = eventName,
            Attempt = current + 1,
            Payload = "{}",
            // The processor restored the message's activity, so this is the original trace (spec 12 D8).
            TraceParent = Activity.Current?.Id,
            ScheduledAt = DateTimeOffset.UtcNow + RetryDelay,
        });

        return Attempt.Create(current + 1)
            .Then(IScheduleEvent.Response (next) => new IScheduleEvent.Response.Scheduled(next));
    }
}
