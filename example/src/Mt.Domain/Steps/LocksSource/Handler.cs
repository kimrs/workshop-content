using Microsoft.Extensions.Logging;
using Mt.Domain;
using Mt.Domain.Migrations;
using Mt.Domain.Steps.Transforms;
using Mt.Results;

namespace Mt.Domain.Steps.LocksSource;

/// <summary>
/// Locks Source, then fans in to <c>Transform</c> once Source, Target and export are all
/// done. Follows the retryable pattern in §6.5: a fault at Source is an outcome, answered
/// with a retry (spec 11). This slice and <c>LocksTarget</c> are intentional near-duplicates
/// — do not DRY them (§4.3).
/// </summary>
public sealed partial class Handler(
    IFetchMigration fetchMigration,
    ILockSource lockSource,
    ISetSourceLocked setSourceLocked,
    IAdd outbox,
    IScheduleEvent scheduleEvent,
    Settings settings,
    ILogger<Handler> logger) : IHandleDomainEvent
{
    public DomainEvent EventType { get; } = new SourceLockRequested();

    public async Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var fetched = await fetchMigration.HandleAsync(migrationId, ct);
        return await fetched.ThenAsync(response => response switch
        {
            IFetchMigration.Response.Proceed => LockAsync(migrationId, ct),
            IFetchMigration.Response.DoNotProceed => Done.Task,
        });
    }

    private async Task<Result<ValueTuple>> LockAsync(Id migrationId, CancellationToken ct)
    {
        var locked = await lockSource.HandleAsync(migrationId, ct);
        return await locked.ThenAsync(response => response switch
        {
            ILockSource.Response.Locked => AdvanceAsync(migrationId, ct),
            ILockSource.Response.Faulted(var reason) => ScheduleRetryAsync(migrationId, reason, ct),
        });
    }

    private async Task<Result<ValueTuple>> AdvanceAsync(Id migrationId, CancellationToken ct)
    {
        var flagged = await setSourceLocked.HandleAsync(migrationId, ct);
        return await flagged.ThenAsync(response => FanInAsync(migrationId, response, ct));
    }

    private async Task<Result<ValueTuple>> ScheduleRetryAsync(Id migrationId, string reason, CancellationToken ct)
    {
        var scheduled = await scheduleEvent.HandleAsync(migrationId, new SourceLockRequested(), settings.MaxAttempts, ct);
        return scheduled.Then(response => response switch
        {
            IScheduleEvent.Response.Scheduled(var next) => RetryScheduled(migrationId, next, reason),
            IScheduleEvent.Response.Exhausted => OutOfRetries(migrationId, reason),
        });
    }

    private Result<ValueTuple> RetryScheduled(Id migrationId, Attempt next, string reason)
    {
        Log.RetryScheduled(logger, migrationId.Value, next.Value, reason);
        return Done.Result;
    }

    private Result<ValueTuple> OutOfRetries(Id migrationId, string reason)
    {
        Log.OutOfRetries(logger, settings.MaxAttempts, migrationId.Value, reason);
        return new OutOfRetriesFailure(
            $"SourceLock exhausted {settings.MaxAttempts} attempts for migration {migrationId.Value}.");
    }

    // Fan-in: the flag write reports when Source, Target and export are all done (§6.5, spec 9).
    private async Task<Result<ValueTuple>> FanInAsync(
        Id migrationId, ISetSourceLocked.Response response, CancellationToken ct)
    {
        return response switch
        {
            ISetSourceLocked.Response.SetupComplete => await RequestTransformAsync(migrationId, ct),
            ISetSourceLocked.Response.SetupIncomplete => Done.Result,
        };
    }

    private async Task<Result<ValueTuple>> RequestTransformAsync(Id migrationId, CancellationToken ct)
    {
        Log.FanInAdvanced(logger, migrationId.Value);
        return await outbox.HandleAsync(migrationId, new TransformRequested(), ct);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = LogEvents.StepRetryScheduled, EventName = nameof(LogEvents.StepRetryScheduled),
            Level = LogLevel.Warning, Message = "SourceLock faulted for migration {MigrationId}; scheduling attempt {NextAttempt}. Reason: {Reason}")]
        public static partial void RetryScheduled(ILogger logger, long migrationId, int nextAttempt, string reason);

        [LoggerMessage(EventId = LogEvents.StepOutOfRetries, EventName = nameof(LogEvents.StepOutOfRetries),
            Level = LogLevel.Error, Message = "SourceLock exhausted {MaxAttempts} attempts for migration {MigrationId}. Reason: {Reason}")]
        public static partial void OutOfRetries(ILogger logger, int maxAttempts, long migrationId, string reason);

        [LoggerMessage(EventId = LogEvents.FanInAdvanced, EventName = nameof(LogEvents.FanInAdvanced),
            Level = LogLevel.Information, Message = "Setup complete for migration {MigrationId}; requesting Transform.")]
        public static partial void FanInAdvanced(ILogger logger, long migrationId);
    }
}
