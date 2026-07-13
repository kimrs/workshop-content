using Microsoft.Extensions.Logging;
using Mt.Domain;
using Mt.Domain.Migrations;
using Mt.Domain.Stages.UnlocksSource;
using Mt.Persistence.Migrations;
using Mt.Results;

namespace Mt.Persistence.Stages.UnlocksSource;

/// <summary>
/// Loads the migration and maps it to this stage's response (specs 4/5/9): a terminal state
/// and an already-unlocked Source are normal skip outcomes (logged here), any state other
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
            case Unlocking { SourceUnlocked: true } or Cancelling { SourceUnlocked: true }:
                Log.AlreadyUnlocked(logger, migrationId.Value);
                return new IFetchMigration.Response.DoNotProceed();
            case Unlocking or Cancelling:
                return new IFetchMigration.Response.Proceed();
            default:
                return new MigrationHasIncorrectState(
                    $"SourceUnlock expected migration {migrationId.Value} to be Unlocking or Cancelling but was {migration.GetType().Name}.");
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = LogEvents.StageAlreadyDone, EventName = nameof(LogEvents.StageAlreadyDone),
            Level = LogLevel.Debug, Message = "SourceUnlock for migration {MigrationId} already finalized; skipping.")]
        public static partial void AlreadyFinalized(ILogger logger, long migrationId);

        [LoggerMessage(EventId = LogEvents.StageAlreadyDone, EventName = nameof(LogEvents.StageAlreadyDone),
            Level = LogLevel.Debug, Message = "Source already unlocked for migration {MigrationId}; skipping.")]
        public static partial void AlreadyUnlocked(ILogger logger, long migrationId);
    }
}
