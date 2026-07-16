using Microsoft.Extensions.Logging;
using Mt.Domain;
using Mt.Domain.Migrations;
using Mt.Domain.Steps.UnlocksTarget;
using Mt.Persistence.Migrations;
using Mt.Results;

namespace Mt.Persistence.Steps.UnlocksTarget;

/// <summary>
/// Loads the migration and maps it to this step's response (specs 4/5/9): a terminal state
/// and an already-unlocked Target are normal skip outcomes (logged here), any state other
/// than <see cref="Unlocking"/> or <see cref="Cancelling"/> is a failure.
/// </summary>
public sealed partial class FetchMigration(WorkshopDbContext db, ILogger<FetchMigration> logger) : IFetchMigration
{
    public async Task<Result<IFetchMigration.Response>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var loaded = await db.LoadMigrationAsync(migrationId.Value, ct);
        return loaded.Then(row => ToResponse(migrationId, row.ToDomain()));
    }

    private Result<IFetchMigration.Response> ToResponse(Id migrationId, Migration migration)
    {
        switch (migration)
        {
            case Completed or Cancelled:
                Log.AlreadyFinalized(logger, migrationId.Value);
                return new IFetchMigration.Response.DoNotProceed();
            case Unlocking { TargetUnlocked: true } or Cancelling { TargetUnlocked: true }:
                Log.AlreadyUnlocked(logger, migrationId.Value);
                return new IFetchMigration.Response.DoNotProceed();
            case Unlocking or Cancelling:
                return new IFetchMigration.Response.Proceed();
            default:
                return new MigrationHasIncorrectState(
                    $"TargetUnlock expected migration {migrationId.Value} to be Unlocking or Cancelling but was {migration.GetType().Name}.");
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = LogEvents.StepAlreadyDone, EventName = nameof(LogEvents.StepAlreadyDone),
            Level = LogLevel.Debug, Message = "TargetUnlock for migration {MigrationId} already finalized; skipping.")]
        public static partial void AlreadyFinalized(ILogger logger, long migrationId);

        [LoggerMessage(EventId = LogEvents.StepAlreadyDone, EventName = nameof(LogEvents.StepAlreadyDone),
            Level = LogLevel.Debug, Message = "Target already unlocked for migration {MigrationId}; skipping.")]
        public static partial void AlreadyUnlocked(ILogger logger, long migrationId);
    }
}
