using Microsoft.Extensions.Logging;
using Mt.Domain;
using Mt.Domain.ExternalIds;
using Mt.Results;

namespace Mt.Portal;

/// <summary>
/// The final "done" notification (§6.3): shows the user the terminal status of their migration —
/// for the workshop it simply logs it, addressed by the Portal's own carrier pigeon (spec 8).
/// Invoked from the unlock fan-in inside the same transaction that persists the terminal state,
/// so the notification never runs ahead of the transition (§10). Must run before the ids are
/// released — the pigeon row has to still be active.
/// </summary>
public sealed partial class NotifyCompletion(IFetch fetchExternalId, ILogger<NotifyCompletion> logger) : INotifyCompletion
{
    public async Task<Result<ValueTuple>> HandleAsync(INotifyCompletion.Request request, CancellationToken ct)
    {
        var pigeon = await fetchExternalId.HandleAsync(
            new IFetch.Request(request.MigrationId, ExternalSystem.Portal, IdType.CarrierPigeon), ct);
        return pigeon.Then(id => Notify(request, id.Value));
    }

    private Result<ValueTuple> Notify(INotifyCompletion.Request request, IdValue pigeonTag)
    {
        switch (request)
        {
            case INotifyCompletion.Request.Migrated:
                Log.Migrated(logger, pigeonTag.Value);
                break;
            case INotifyCompletion.Request.Cancelled:
                Log.Cancelled(logger, pigeonTag.Value);
                break;
        }

        return default(ValueTuple);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = LogEvents.MigrationFinalized, EventName = nameof(LogEvents.MigrationFinalized),
            Level = LogLevel.Information, Message = "✅ [SIM] Pigeon {PigeonTag} dispatched: migrated to Target.")]
        public static partial void Migrated(ILogger logger, string pigeonTag);

        [LoggerMessage(EventId = LogEvents.MigrationFinalized, EventName = nameof(LogEvents.MigrationFinalized),
            Level = LogLevel.Information, Message = "🛑 [SIM] Pigeon {PigeonTag} dispatched: migration cancelled.")]
        public static partial void Cancelled(ILogger logger, string pigeonTag);
    }
}
