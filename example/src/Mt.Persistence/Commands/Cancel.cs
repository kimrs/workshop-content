using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mt.Domain;
using Mt.Domain.Migrations;
using Mt.Domain.Steps;
using Mt.Domain.Steps.UnlocksSource;
using Mt.Domain.Steps.UnlocksTarget;
using Mt.Persistence.Migrations;
using Mt.Persistence.Rows;
using Mt.Results;

namespace Mt.Persistence.Commands;

/// <summary>
/// Requires an <see cref="ICancellable"/> state. Transitions to <c>Cancelling</c> and writes an
/// unlock event for each system that was actually locked, in one transaction (§9). If nothing was
/// locked yet, teardown is already complete, so it finalizes to <c>Cancelled</c> and notifies.
/// </summary>
public sealed partial class Cancel(
    WorkshopDbContext db,
    Mt.Domain.ExternalIds.ICancel cancelExternalIds,
    INotifyCompletion notifyCompletion,
    ILogger<Cancel> logger) : Mt.Domain.Commands.ICancel
{
    public async Task<Result<Id>> HandleAsync(OrganizationNumber organizationNumber, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var row = await db.Migrations.FirstOrDefaultAsync(
            m => m.OrganizationNumber == organizationNumber.Value
                && m.State != MigrationState.Completed
                && m.State != MigrationState.Cancelled,
            ct);
        if (row is null)
        {
            return new NotFoundFailure($"No active migration for {organizationNumber.Value}.");
        }

        if (row.ToDomain() is not ICancellable cancellable)
        {
            return new MigrationHasIncorrectState(
                $"Cancel requires migration {row.Id} to be in a cancellable state but was {row.State}.");
        }

        var cancelledResult = cancellable.Cancel();
        if (cancelledResult.IsFailed(out var cancelling, out var failures))
        {
            return failures;
        }

        row.SourceUnlocked = cancelling.SourceUnlocked;
        row.TargetUnlocked = cancelling.TargetUnlocked;

        if (cancelling.IsFullyUnlocked)
        {
            // Nothing was locked (cancelled straight from Created) — finalize immediately.
            // This terminal transition skips the unlock finalizers, so the external ids are
            // released here (spec 8).
            row.State = MigrationState.Cancelled;
            await db.SaveChangesAsync(ct);
            // Notify first: the Portal still needs its active pigeon row (spec 8).
            var notified = await notifyCompletion.HandleAsync(new INotifyCompletion.Request.Cancelled(cancelling.Id), ct);
            var finalized = await notified.ThenAsync(_ => cancelExternalIds.HandleAsync(cancelling.Id, ct));
            if (finalized.IsFailed(out _, out var finalizeFailures))
            {
                return finalizeFailures;
            }

            await transaction.CommitAsync(ct);
            Log.CancelledImmediately(logger, row.Id);
            return Id.Create(row.Id);
        }

        row.State = MigrationState.Cancelling;
        if (!cancelling.SourceUnlocked)
        {
            AddUnlockEvent(row.Id, new SourceUnlockRequested());
        }

        if (!cancelling.TargetUnlocked)
        {
            AddUnlockEvent(row.Id, new TargetUnlockRequested());
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        Log.Cancelling(logger, row.Id);
        return Id.Create(row.Id);
    }

    private void AddUnlockEvent(long migrationId, DomainEvent domainEvent) =>
        db.Outbox.Add(new OutboxRow
        {
            MigrationId = migrationId,
            DomainEvent = domainEvent.ToMessageType(),
            Attempt = Attempt.First.Value,
            Payload = "{}",
            TraceParent = Activity.Current?.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        });

    private static partial class Log
    {
        [LoggerMessage(EventId = LogEvents.MigrationCancelRequested, EventName = nameof(LogEvents.MigrationCancelRequested),
            Level = LogLevel.Information, Message = "🛑 Cancelled migration {MigrationId} (nothing was locked).")]
        public static partial void CancelledImmediately(ILogger logger, long migrationId);

        [LoggerMessage(EventId = LogEvents.MigrationCancelRequested, EventName = nameof(LogEvents.MigrationCancelRequested),
            Level = LogLevel.Information, Message = "🛑 Cancelling migration {MigrationId}; releasing locked systems.")]
        public static partial void Cancelling(ILogger logger, long migrationId);
    }
}
