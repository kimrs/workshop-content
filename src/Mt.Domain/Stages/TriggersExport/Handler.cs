using Microsoft.Extensions.Logging;
using Mt.Domain;
using Mt.Domain.Migrations;
using Mt.Domain.Stages.Transforms;
using Mt.Results;

namespace Mt.Domain.Stages.TriggersExport;

/// <summary>
/// Triggers the SAF-T export on Source, then fans in to <c>Transform</c> once Source,
/// Target and export are all done (§6.5): a fault at Source is an outcome, answered with a
/// retry (spec 11). Third setup sibling alongside the two lock slices — same retryable
/// shape, kept separate (§4.3).
/// </summary>
public sealed partial class Handler(
    IFetchMigration fetchMigration,
    ITriggerExport triggerExport,
    ISetExportTriggered setExportTriggered,
    IAdd outbox,
    IScheduleEvent scheduleEvent,
    Settings settings,
    ILogger<Handler> logger) : IHandleDomainEvent
{
    public DomainEvent EventType { get; } = new ExportRequested();

    public async Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var fetched = await fetchMigration.HandleAsync(migrationId, ct);
        return await fetched.ThenAsync(response => response switch
        {
            IFetchMigration.Response.Proceed => TriggerAsync(migrationId, ct),
            _ => Done.Task,
        });
    }

    private async Task<Result<ValueTuple>> TriggerAsync(Id migrationId, CancellationToken ct)
    {
        var triggered = await triggerExport.HandleAsync(migrationId, ct);
        return await triggered.ThenAsync(response => response switch
        {
            ITriggerExport.Response.Faulted(var reason) => ScheduleRetryAsync(migrationId, reason, ct),
            _ => AdvanceAsync(migrationId, ct),
        });
    }

    private async Task<Result<ValueTuple>> AdvanceAsync(Id migrationId, CancellationToken ct)
    {
        var flagged = await setExportTriggered.HandleAsync(migrationId, ct);
        return await flagged.ThenAsync(response => FanInAsync(migrationId, response, ct));
    }

    private async Task<Result<ValueTuple>> ScheduleRetryAsync(Id migrationId, string reason, CancellationToken ct)
    {
        var scheduled = await scheduleEvent.HandleAsync(migrationId, new ExportRequested(), settings.MaxAttempts, ct);
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
            $"ExportTrigger exhausted {settings.MaxAttempts} attempts for migration {migrationId.Value}.");
    }

    // Fan-in: the flag write reports when Source, Target and export are all done (§6.5, spec 9).
    private async Task<Result<ValueTuple>> FanInAsync(
        Id migrationId, ISetExportTriggered.Response response, CancellationToken ct)
    {
        if (response is not ISetExportTriggered.Response.SetupComplete)
        {
            return Done.Result;
        }

        Log.FanInAdvanced(logger, migrationId.Value);
        return await outbox.HandleAsync(migrationId, new TransformRequested(), ct);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = LogEvents.StageRetryScheduled, EventName = nameof(LogEvents.StageRetryScheduled),
            Level = LogLevel.Warning, Message = "ExportTrigger faulted for migration {MigrationId}; scheduling attempt {NextAttempt}. Reason: {Reason}")]
        public static partial void RetryScheduled(ILogger logger, long migrationId, int nextAttempt, string reason);

        [LoggerMessage(EventId = LogEvents.StageOutOfRetries, EventName = nameof(LogEvents.StageOutOfRetries),
            Level = LogLevel.Error, Message = "ExportTrigger exhausted {MaxAttempts} attempts for migration {MigrationId}. Reason: {Reason}")]
        public static partial void OutOfRetries(ILogger logger, int maxAttempts, long migrationId, string reason);

        [LoggerMessage(EventId = LogEvents.FanInAdvanced, EventName = nameof(LogEvents.FanInAdvanced),
            Level = LogLevel.Information, Message = "Setup complete for migration {MigrationId}; requesting Transform.")]
        public static partial void FanInAdvanced(ILogger logger, long migrationId);
    }
}
