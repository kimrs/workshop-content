using System.Diagnostics;
using Mt.Domain;
using Mt.Domain.Migrations;
using Mt.Domain.Stages;
using Mt.Persistence.Rows;
using Mt.Results;

namespace Mt.Persistence.Outboxes;

/// <summary>
/// Adds the next event to the outbox in the caller's unit of work — no <c>SaveChanges</c>
/// here; the surrounding transaction (inbox <c>ExecuteOnce</c> or a command) commits (§8.1).
/// Captures the current W3C trace context for correlation.
/// </summary>
public sealed class Add(WorkshopDbContext db) : IAdd
{
    public Task<Result<ValueTuple>> HandleAsync(Id migrationId, DomainEvent domainEvent, CancellationToken ct)
    {
        db.Outbox.Add(new OutboxRow
        {
            MigrationId = migrationId.Value,
            DomainEvent = domainEvent.ToMessageType(),
            Attempt = Attempt.First.Value,
            Payload = "{}",
            TraceParent = Activity.Current?.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        return Task.FromResult<Result<ValueTuple>>(default(ValueTuple));
    }
}
