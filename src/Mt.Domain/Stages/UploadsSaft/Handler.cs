using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Stages.UploadsSaft;

/// <summary>
/// Downloads the SAF-T file from Source and uploads it to Target, advancing
/// <c>PreSaftUploaded → SaftUploaded</c>. Emits nothing — this is the manual-approval
/// gate (§1.3, §6.5). Skips gracefully if cancelled concurrently.
/// </summary>
public sealed class Handler(
    IFetchMigration fetchMigration,
    IDownloadSaft downloadSaft,
    IUploadSaft uploadSaft,
    ISetSaftUploaded setSaftUploaded) : IHandleDomainEvent
{
    public DomainEvent EventType { get; } = new SaftUploadRequested();

    public async Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var fetched = await fetchMigration.HandleAsync(migrationId, ct);
        return await fetched.ThenAsync(response => response switch
        {
            IFetchMigration.Response.Proceed(var preSaftUploaded) => UploadAsync(migrationId, preSaftUploaded, ct),
            _ => Done.Task,
        });
    }

    // Manual gate: persist SaftUploaded and stop. Nothing is emitted until POST /Approve.
    private async Task<Result<ValueTuple>> UploadAsync(Id migrationId, PreSaftUploaded preSaftUploaded, CancellationToken ct)
    {
        var downloaded = await downloadSaft.HandleAsync(migrationId, ct);
        return await downloaded
            .ThenAsync(saftFile => uploadSaft.HandleAsync(migrationId, saftFile, ct))
            .Then(_ => preSaftUploaded.UploadSaft())
            .ThenAsync(_ => setSaftUploaded.HandleAsync(migrationId, ct));
    }
}
