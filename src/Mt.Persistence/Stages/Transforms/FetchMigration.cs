using Microsoft.Extensions.Logging;
using Mt.Domain;
using Mt.Domain.Migrations;
using Mt.Domain.Stages.Transforms;
using Mt.Persistence.Migrations;
using Mt.Results;

namespace Mt.Persistence.Stages.Transforms;

/// <summary>
/// Loads the migration and maps it to this stage's response (specs 4/5/9): cancellation is a
/// normal skip outcome (logged here), any state other than <see cref="Prepared"/> — including
/// a <see cref="Created"/> whose setup is incomplete — is a failure.
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
            case Prepared prepared:
                return new IFetchMigration.Response.Proceed(prepared);
            default:
                return new MigrationHasIncorrectState(
                    $"Transform expected migration {migrationId.Value} to be Prepared but was {migration.GetType().Name}.");
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = LogEvents.StageSkippedCancelled, EventName = nameof(LogEvents.StageSkippedCancelled),
            Level = LogLevel.Warning, Message = "Skipping Transform for cancelled migration {MigrationId}.")]
        public static partial void SkippedCancelled(ILogger logger, long migrationId);
    }
}
