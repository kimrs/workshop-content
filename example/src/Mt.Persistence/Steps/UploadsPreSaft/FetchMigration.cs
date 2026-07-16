using Microsoft.Extensions.Logging;
using Mt.Domain;
using Mt.Domain.Migrations;
using Mt.Domain.Steps.UploadsPreSaft;
using Mt.Persistence.Migrations;
using Mt.Results;

namespace Mt.Persistence.Steps.UploadsPreSaft;

/// <summary>
/// Loads the migration and maps it to this step's response (specs 4/5/9): cancellation is a
/// normal skip outcome (logged here), any state other than <see cref="Transformed"/> is a
/// failure.
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
            case Transformed transformed:
                return new IFetchMigration.Response.Proceed(transformed);
            default:
                return new MigrationHasIncorrectState(
                    $"PreSaftUpload expected migration {migrationId.Value} to be Transformed but was {migration.GetType().Name}.");
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = LogEvents.StepSkippedCancelled, EventName = nameof(LogEvents.StepSkippedCancelled),
            Level = LogLevel.Warning, Message = "Skipping PreSaftUpload for cancelled migration {MigrationId}.")]
        public static partial void SkippedCancelled(ILogger logger, long migrationId);
    }
}
