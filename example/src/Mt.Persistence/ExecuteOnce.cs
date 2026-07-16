using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Mt.Domain;
using Mt.Domain.Migrations;
using Mt.Domain.Steps;
using Mt.Persistence.Rows;
using Mt.Results;

namespace Mt.Persistence;

/// <summary>
/// Runs a message through the inbox for idempotency, then dispatches to the matching handler —
/// all in one DB transaction (§8.4). Deduplication is on <c>(MigrationId, DomainEvent, Attempt)</c>;
/// there is no <c>MessageId</c> (§2.2). A handler failure rolls the work back and then records
/// the abort on the inbox row in its own transaction (spec 12 D4), so "a human looks" has
/// something durable to look at and a redelivered attempt is not pointlessly re-run.
/// </summary>
public sealed partial class ExecuteOnce(
    WorkshopDbContext db,
    IEnumerable<IHandleDomainEvent> handlers,
    ILogger<ExecuteOnce> logger)
{
    public async Task<Result<ValueTuple>> ExecuteOnceAsync(
        Id migrationId,
        DomainEvent domainEvent,
        Attempt attempt,
        CancellationToken ct)
    {
        var eventName = domainEvent.ToMessageType();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // (2) Already processed this exact attempt? Skip — an aborted attempt stays aborted.
        var existing = await db.Inbox.FindAsync([migrationId.Value, eventName, attempt.Value], ct);
        if (existing is not null)
        {
            if (existing.FailedAt is not null)
            {
                Log.PreviouslyFailedSkipped(logger, eventName, migrationId.Value, attempt.Value);
            }
            else
            {
                Log.DuplicateSkipped(logger, eventName, migrationId.Value, attempt.Value);
            }

            await transaction.RollbackAsync(ct);
            return default(ValueTuple);
        }

        // (3) Claim the message.
        db.Inbox.Add(new InboxRow
        {
            MigrationId = migrationId.Value,
            DomainEvent = eventName,
            Attempt = attempt.Value,
            ReceivedAt = DateTimeOffset.UtcNow,
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // (7) Concurrent duplicate lost the race — treat as already processed.
            Log.ConcurrentDuplicateSkipped(logger, eventName, migrationId.Value, attempt.Value);
            await transaction.RollbackAsync(ct);
            return default(ValueTuple);
        }

        // (4) Safe handler lookup — never Single() (§8.4, §10).
        var handlerResult = ResolveHandler(domainEvent);
        if (handlerResult.IsFailed(out var handler, out var lookupFailures))
        {
            await transaction.RollbackAsync(ct);
            await RecordAbortAsync(migrationId, eventName, attempt, lookupFailures, ct);
            return lookupFailures;
        }

        var handled = await handler.HandleAsync(migrationId, ct);
        if (handled.IsFailed(out _, out var handleFailures))
        {
            // (5) Handler failed — roll back the claim and the work, then record the abort.
            await transaction.RollbackAsync(ct);
            await RecordAbortAsync(migrationId, eventName, attempt, handleFailures, ct);
            return handleFailures;
        }

        // (6) Commit the claim and the handler's state changes together.
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return default(ValueTuple);
    }

    // The abort record is written after the rollback, outside the original transaction: a crash
    // in between just means the redelivery re-aborts and records then — it converges (spec 12 D4).
    private async Task RecordAbortAsync(
        Id migrationId, string eventName, Attempt attempt, Failure[] failures, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        db.Inbox.Add(new InboxRow
        {
            MigrationId = migrationId.Value,
            DomainEvent = eventName,
            Attempt = attempt.Value,
            ReceivedAt = DateTimeOffset.UtcNow,
            FailedAt = DateTimeOffset.UtcNow,
            FailureReason = failures[0].Message,
        });

        try
        {
            await db.SaveChangesAsync(ct);
            Log.AbortRecorded(logger, eventName, migrationId.Value, attempt.Value, failures[0].Message);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // A concurrent delivery recorded this attempt first — either way it is recorded.
            db.ChangeTracker.Clear();
        }
    }

    private Result<IHandleDomainEvent> ResolveHandler(DomainEvent domainEvent)
    {
        var eventName = domainEvent.ToMessageType();
        var matches = handlers.Where(h => h.EventType.ToMessageType() == eventName).ToArray();
        if (matches.Length == 1)
        {
            return new Result<IHandleDomainEvent>.Completed(matches[0]);
        }

        return new NotFoundFailure(
            $"Expected exactly one handler for '{eventName}' but found {matches.Length}.");
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static partial class Log
    {
        [LoggerMessage(EventId = LogEvents.InboxDuplicateSkipped, EventName = nameof(LogEvents.InboxDuplicateSkipped),
            Level = LogLevel.Information, Message = "Duplicate {Event} for migration {MigrationId} attempt {Attempt}; skipping.")]
        public static partial void DuplicateSkipped(ILogger logger, string @event, long migrationId, int attempt);

        [LoggerMessage(EventId = LogEvents.InboxDuplicateSkipped, EventName = nameof(LogEvents.InboxDuplicateSkipped),
            Level = LogLevel.Information, Message = "Concurrent duplicate {Event} for migration {MigrationId} attempt {Attempt}; skipping.")]
        public static partial void ConcurrentDuplicateSkipped(ILogger logger, string @event, long migrationId, int attempt);

        [LoggerMessage(EventId = LogEvents.InboxAbortRecorded, EventName = nameof(LogEvents.InboxAbortRecorded),
            Level = LogLevel.Error, Message = "Recorded abort of {Event} for migration {MigrationId} attempt {Attempt}: {Reason}")]
        public static partial void AbortRecorded(ILogger logger, string @event, long migrationId, int attempt, string reason);

        [LoggerMessage(EventId = LogEvents.InboxAbortRecorded, EventName = nameof(LogEvents.InboxAbortRecorded),
            Level = LogLevel.Warning, Message = "{Event} for migration {MigrationId} attempt {Attempt} previously aborted; skipping redelivery.")]
        public static partial void PreviouslyFailedSkipped(ILogger logger, string @event, long migrationId, int attempt);
    }
}
