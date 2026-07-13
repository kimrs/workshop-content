using Microsoft.Extensions.Logging;
using Mt.Domain;
using Mt.Domain.Migrations;
using Mt.Domain.Stages.UnlocksSource;
using Mt.Domain.Stages.UnlocksTarget;
using Mt.Domain.Stages.UploadsPreSaft;
using Mt.Results;

namespace Mt.Domain.Stages.Transforms;

/// <summary>
/// Fan-in target of setup. Fetches the client and applies the one domain rule: no
/// address ⇒ automatically cancel; otherwise advance to <c>Transformed</c> (§1.2, §6.5).
/// </summary>
public sealed partial class Handler(
    IFetchMigration fetchMigration,
    IFetchClient fetchClient,
    ISetTransformed setTransformed,
    ISetCancelling setCancelling,
    IAdd outbox,
    ILogger<Handler> logger) : IHandleDomainEvent
{
    public DomainEvent EventType { get; } = new TransformRequested();

    public async Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var fetched = await fetchMigration.HandleAsync(migrationId, ct);
        return await fetched.ThenAsync(response => response switch
        {
            IFetchMigration.Response.Proceed(var prepared) => TransformAsync(prepared, ct),
            _ => Done.Task,
        });
    }

    private async Task<Result<ValueTuple>> TransformAsync(Prepared prepared, CancellationToken ct)
    {
        var client = await fetchClient.HandleAsync(prepared.Id, ct);
        return await client.ThenAsync(c => c switch
        {
            Client.WithoutAddress => AutoCancelAsync(prepared, ct),
            _ => AdvanceAsync(prepared, ct),
        });
    }

    // No address ⇒ invalid data ⇒ enter Cancelling and emit both unlock events (§1.2).
    private async Task<Result<ValueTuple>> AutoCancelAsync(Prepared prepared, CancellationToken ct)
    {
        Log.AutoCancelled(logger, prepared.Id.Value);

        return await prepared.Cancel()
            .ThenAsync(_ => setCancelling.HandleAsync(prepared.Id, ct))
            .ThenAsync(_ => outbox.HandleAsync(prepared.Id, new SourceUnlockRequested(), ct))
            .ThenAsync(_ => outbox.HandleAsync(prepared.Id, new TargetUnlockRequested(), ct));
    }

    private async Task<Result<ValueTuple>> AdvanceAsync(Prepared prepared, CancellationToken ct)
    {
        return await prepared.Transform()
            .ThenAsync(_ => setTransformed.HandleAsync(prepared.Id, ct))
            .ThenAsync(_ => outbox.HandleAsync(prepared.Id, new PreSaftUploadRequested(), ct));
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = LogEvents.TransformAutoCancelled, EventName = nameof(LogEvents.TransformAutoCancelled),
            Level = LogLevel.Warning, Message = "Client has no address; auto-cancelling migration {MigrationId}.")]
        public static partial void AutoCancelled(ILogger logger, long migrationId);
    }
}
