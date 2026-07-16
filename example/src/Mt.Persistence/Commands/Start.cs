using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mt.Domain;
using Mt.Domain.Commands;
using Mt.Domain.ExternalIds;
using Mt.Domain.Migrations;
using Mt.Domain.Steps;
using Mt.Domain.Steps.LocksSource;
using Mt.Domain.Steps.LocksTarget;
using Mt.Domain.Steps.TriggersExport;
using Mt.Persistence.ExternalIds;
using Mt.Persistence.Rows;
using Mt.Results;
using IAddExternalId = Mt.Domain.ExternalIds.IAdd;

namespace Mt.Persistence.Commands;

/// <summary>
/// Creates the migration in <c>Created</c>, records its external ids, and writes the three
/// setup events to the outbox in one transaction (§9, spec 8). An id claimed by another
/// active migration rolls the whole command back with <see cref="ExternalIdConflictFailure"/> —
/// the pre-check produces the domain failure; the filtered unique index is the backstop.
/// </summary>
public sealed partial class Start(
    WorkshopDbContext db,
    IAddExternalId addExternalId,
    ILogger<Start> logger) : IStart
{
    public async Task<Result<Id>> HandleAsync(IStart.Request request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var alreadyRunning = await db.Migrations.AnyAsync(
            m => m.OrganizationNumber == request.OrganizationNumber.Value
                && m.State != MigrationState.Completed
                && m.State != MigrationState.Cancelled,
            ct);
        if (alreadyRunning)
        {
            return new DuplicateFailure($"A migration for {request.OrganizationNumber.Value} is already in progress.");
        }

        var row = new MigrationRow
        {
            State = MigrationState.Created,
            OrganizationNumber = request.OrganizationNumber.Value,
        };
        db.Migrations.Add(row);
        await db.SaveChangesAsync(ct); // assigns the generated Id

        var mapped = Id.Create(row.Id)
            .Then(id => request.PunchCardNumber.ToExternalId(id)
                .Then(punchCard => request.HoloCrystalId.ToExternalId(id)
                    .Then(holoCrystal => request.CarrierPigeonTag.ToExternalId(id)
                        .Then(pigeonTag => new[] { punchCard, holoCrystal, pigeonTag }))));
        if (mapped.IsFailed(out var externalIds, out var mapFailures))
        {
            return mapFailures;
        }

        var preChecked = await PreCheckConflictsAsync(externalIds, ct);
        var recorded = await preChecked.ThenAsync(_ => AddExternalIdsAsync(externalIds, ct));
        if (recorded.IsFailed(out _, out var recordFailures))
        {
            return recordFailures;
        }

        AddSetupEvent(row.Id, new SourceLockRequested());
        AddSetupEvent(row.Id, new TargetLockRequested());
        AddSetupEvent(row.Id, new ExportRequested());
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        Log.Started(logger, row.Id);
        return Id.Create(row.Id);
    }

    // The belt is the filtered unique index; this pre-check is the suspenders — a domain
    // failure maps to a clean 409 where a constraint violation would not (spec 8).
    private async Task<Result<ValueTuple>> PreCheckConflictsAsync(ExternalId[] externalIds, CancellationToken ct)
    {
        var values = externalIds.Select(e => e.Value.Value).ToArray();
        var activeRows = await db.ExternalIds
            .Where(row => !row.IsCancelled && values.Contains(row.Value))
            .ToListAsync(ct);

        var clash = activeRows.FirstOrDefault(row => externalIds.Any(e =>
            e.System.Value == row.System && e.Type.Value == row.Name && e.Value.Value == row.Value));
        if (clash is not null)
        {
            return new ExternalIdConflictFailure(
                $"{clash.System} {clash.Name} '{clash.Value}' is already claimed by active migration {clash.MigrationId}.");
        }

        return default(ValueTuple);
    }

    private async Task<Result<ValueTuple>> AddExternalIdsAsync(ExternalId[] externalIds, CancellationToken ct)
    {
        foreach (var externalId in externalIds)
        {
            var added = await addExternalId.HandleAsync(
                new IAddExternalId.Request(externalId.MigrationId, externalId.System, externalId.Type, externalId.Value), ct);
            if (added.IsFailed(out _, out var failures))
            {
                return failures;
            }
        }

        return default(ValueTuple);
    }

    private void AddSetupEvent(long migrationId, DomainEvent domainEvent) =>
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
        [LoggerMessage(EventId = LogEvents.MigrationStarted, EventName = nameof(LogEvents.MigrationStarted),
            Level = LogLevel.Information, Message = "🚀 Started migration {MigrationId}; setup fanned out.")]
        public static partial void Started(ILogger logger, long migrationId);
    }
}
