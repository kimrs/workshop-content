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
/// Requires <c>SaftUploaded</c>. Transitions to <c>Unlocking</c> and writes both unlock events
/// at <see cref="Attempt.First"/> in one transaction (§9).
/// </summary>
public sealed partial class Approve(WorkshopDbContext db, ILogger<Approve> logger) : Mt.Domain.Commands.IApprove
{
    public async Task<Result<Id>> HandleAsync(OrganizationNumber organizationNumber, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var row = await FindActiveAsync(organizationNumber, ct);
        if (row is null)
        {
            return new NotFoundFailure($"No active migration for {organizationNumber.Value}.");
        }

        if (row.ToDomain() is not SaftUploaded saftUploaded)
        {
            return new MigrationHasIncorrectState(
                $"Approve requires migration {row.Id} to be SaftUploaded but was {row.State}.");
        }

        var approved = saftUploaded.Approve();
        if (approved.IsFailed(out var unlocking, out var failures))
        {
            return failures;
        }

        row.State = MigrationState.Unlocking;
        row.SourceUnlocked = unlocking.SourceUnlocked;
        row.TargetUnlocked = unlocking.TargetUnlocked;
        AddUnlockEvent(row.Id, new SourceUnlockRequested());
        AddUnlockEvent(row.Id, new TargetUnlockRequested());
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        Log.Approved(logger, row.Id);
        return Id.Create(row.Id);
    }

    private Task<MigrationRow?> FindActiveAsync(OrganizationNumber organizationNumber, CancellationToken ct) =>
        db.Migrations.FirstOrDefaultAsync(
            m => m.OrganizationNumber == organizationNumber.Value
                && m.State != MigrationState.Completed
                && m.State != MigrationState.Cancelled,
            ct);

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
        [LoggerMessage(EventId = LogEvents.MigrationApproved, EventName = nameof(LogEvents.MigrationApproved),
            Level = LogLevel.Information, Message = "👍 Approved migration {MigrationId}; teardown fanned out.")]
        public static partial void Approved(ILogger logger, long migrationId);
    }
}
