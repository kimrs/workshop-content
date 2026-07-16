using Microsoft.Extensions.Logging;
using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Steps.UnlocksTarget;

/// <summary>
/// Unlocks Target during teardown: a fault at Target is an outcome, answered with a retry
/// (spec 11). Whichever unlock finishes last fans in to the terminal state and notifies
/// completion (§6.5). Intentional near-duplicate of <c>UnlocksSource</c> (§4.3). Runs for
/// both the approve path (<c>Unlocking</c>) and the cancel path (<c>Cancelling</c>).
/// </summary>
public sealed partial class Handler(
    IFetchMigration fetchMigration,
    IUnlockTarget unlockTarget,
    ISetTargetUnlocked setTargetUnlocked,
    ISetCompleted setCompleted,
    ISetCancelled setCancelled,
    ExternalIds.ICancel cancelExternalIds,
    INotifyCompletion notifyCompletion,
    IScheduleEvent scheduleEvent,
    Settings settings,
    ILogger<Handler> logger) : IHandleDomainEvent
{
    public DomainEvent EventType { get; } = new TargetUnlockRequested();

    public async Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var fetched = await fetchMigration.HandleAsync(migrationId, ct);
        return await fetched.ThenAsync(response => response switch
        {
            IFetchMigration.Response.Proceed => UnlockAsync(migrationId, ct),
            _ => Done.Task,
        });
    }

    private async Task<Result<ValueTuple>> UnlockAsync(Id migrationId, CancellationToken ct)
    {
        var unlocked = await unlockTarget.HandleAsync(migrationId, ct);
        return await unlocked.ThenAsync(response => response switch
        {
            IUnlockTarget.Response.Faulted(var reason) => ScheduleRetryAsync(migrationId, reason, ct),
            _ => AdvanceAsync(migrationId, ct),
        });
    }

    private async Task<Result<ValueTuple>> AdvanceAsync(Id migrationId, CancellationToken ct)
    {
        var flagged = await setTargetUnlocked.HandleAsync(migrationId, ct);
        return await flagged.ThenAsync(response => FanInAsync(response, ct));
    }

    private async Task<Result<ValueTuple>> ScheduleRetryAsync(Id migrationId, string reason, CancellationToken ct)
    {
        var scheduled = await scheduleEvent.HandleAsync(migrationId, new TargetUnlockRequested(), settings.MaxAttempts, ct);
        return scheduled.Then(response => response switch
        {
            IScheduleEvent.Response.Scheduled(var next) => RetryScheduled(migrationId, next, reason),
            _ => OutOfRetries(migrationId, reason),
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
            $"TargetUnlock exhausted {settings.MaxAttempts} attempts for migration {migrationId.Value}.");
    }

    // Fan-in: the flag write reports when both unlocks are done, and on which path;
    // finalize the terminal state and notify (§6.5, spec 9).
    private async Task<Result<ValueTuple>> FanInAsync(ISetTargetUnlocked.Response response, CancellationToken ct)
    {
        switch (response)
        {
            case ISetTargetUnlocked.Response.Complete(var unlocking):
                return await FinalizeCompletedAsync(unlocking, ct);
            case ISetTargetUnlocked.Response.Cancel(var cancelling):
                return await FinalizeCancelledAsync(cancelling, ct);
            default:
                return Done.Result;
        }
    }

    private async Task<Result<ValueTuple>> FinalizeCompletedAsync(Unlocking unlocking, CancellationToken ct)
        => await unlocking.Complete()
            .ThenAsync(_ => setCompleted.HandleAsync(unlocking.Id, ct))
            .ThenAsync(_ => notifyCompletion.HandleAsync(new INotifyCompletion.Request.Migrated(unlocking.Id), ct))
            .Then(_ => LogCompleted(unlocking.Id));

    private async Task<Result<ValueTuple>> FinalizeCancelledAsync(Cancelling cancelling, CancellationToken ct)
        => await cancelling.FinalizeCancellation()
            .ThenAsync(_ => setCancelled.HandleAsync(cancelling.Id, ct))
            .ThenAsync(_ => notifyCompletion.HandleAsync(new INotifyCompletion.Request.Cancelled(cancelling.Id), ct))
            // Release the ids last: the Portal notification still needs its active pigeon row (spec 8).
            .ThenAsync(_ => cancelExternalIds.HandleAsync(cancelling.Id, ct))
            .Then(_ => LogCancelled(cancelling.Id));

    private Result<ValueTuple> LogCompleted(Id migrationId)
    {
        Log.MigrationCompleted(logger, migrationId.Value);
        return Done.Result;
    }

    private Result<ValueTuple> LogCancelled(Id migrationId)
    {
        Log.MigrationCancelled(logger, migrationId.Value);
        return Done.Result;
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = LogEvents.StepRetryScheduled, EventName = nameof(LogEvents.StepRetryScheduled),
            Level = LogLevel.Warning, Message = "TargetUnlock faulted for migration {MigrationId}; scheduling attempt {NextAttempt}. Reason: {Reason}")]
        public static partial void RetryScheduled(ILogger logger, long migrationId, int nextAttempt, string reason);

        [LoggerMessage(EventId = LogEvents.StepOutOfRetries, EventName = nameof(LogEvents.StepOutOfRetries),
            Level = LogLevel.Error, Message = "TargetUnlock exhausted {MaxAttempts} attempts for migration {MigrationId}. Reason: {Reason}")]
        public static partial void OutOfRetries(ILogger logger, int maxAttempts, long migrationId, string reason);

        [LoggerMessage(EventId = LogEvents.MigrationFinalized, EventName = nameof(LogEvents.MigrationFinalized),
            Level = LogLevel.Information, Message = "Migration {MigrationId} completed.")]
        public static partial void MigrationCompleted(ILogger logger, long migrationId);

        [LoggerMessage(EventId = LogEvents.MigrationFinalized, EventName = nameof(LogEvents.MigrationFinalized),
            Level = LogLevel.Information, Message = "Migration {MigrationId} cancelled.")]
        public static partial void MigrationCancelled(ILogger logger, long migrationId);
    }
}
