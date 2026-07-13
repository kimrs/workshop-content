using Microsoft.Extensions.Logging;
using Mt.Domain;
using Mt.Domain.Migrations;
using Mt.Domain.Stages.TriggersExport;
using Mt.Persistence.Migrations;
using Mt.Results;

namespace Mt.Persistence.Stages.TriggersExport;

/// <summary>
/// Loads the migration and maps it to this stage's response (specs 4/5/9): cancellation and
/// an already-triggered export are normal skip outcomes (logged here), any state other than
/// <see cref="Created"/> is a failure.
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
            case Cancelling or Cancelled:
                Log.SkippedCancelled(logger, migrationId.Value);
                return new IFetchMigration.Response.DoNotProceed();
            case Created { ExportTriggered: true }:
                Log.AlreadyDone(logger, migrationId.Value);
                return new IFetchMigration.Response.DoNotProceed();
            case Created:
                return new IFetchMigration.Response.Proceed();
            default:
                return new MigrationHasIncorrectState(
                    $"ExportTrigger expected migration {migrationId.Value} to be Created but was {migration.GetType().Name}.");
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = LogEvents.StageSkippedCancelled, EventName = nameof(LogEvents.StageSkippedCancelled),
            Level = LogLevel.Warning, Message = "Skipping ExportTrigger for cancelled migration {MigrationId}.")]
        public static partial void SkippedCancelled(ILogger logger, long migrationId);

        [LoggerMessage(EventId = LogEvents.StageAlreadyDone, EventName = nameof(LogEvents.StageAlreadyDone),
            Level = LogLevel.Debug, Message = "Export already triggered for migration {MigrationId}; skipping.")]
        public static partial void AlreadyDone(ILogger logger, long migrationId);
    }
}
