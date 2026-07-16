using Mt.Domain;
using Mt.Domain.Migrations;
using Mt.Domain.Steps.UploadsSaft;
using Mt.Results;

namespace Mt.Domain.Steps.UploadsPreSaft;

/// <summary>
/// Uploads the pre-SAF-T work to Target and advances <c>Transformed → PreSaftUploaded</c>,
/// then requests the SAF-T upload (§6.5). Skips gracefully if cancelled concurrently.
/// </summary>
public sealed class Handler(
    IFetchMigration fetchMigration,
    IUploadPreSaft uploadPreSaft,
    ISetPreSaftUploaded setPreSaftUploaded,
    IAdd outbox) : IHandleDomainEvent
{
    public DomainEvent EventType { get; } = new PreSaftUploadRequested();

    public async Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var fetched = await fetchMigration.HandleAsync(migrationId, ct);
        return await fetched.ThenAsync(response => response switch
        {
            IFetchMigration.Response.Proceed(var transformed) => UploadAsync(migrationId, transformed, ct),
            _ => Done.Task,
        });
    }

    private async Task<Result<ValueTuple>> UploadAsync(Id migrationId, Transformed transformed, CancellationToken ct)
    {
        var uploaded = await uploadPreSaft.HandleAsync(migrationId, ct);
        return await uploaded
            .Then(_ => transformed.UploadPreSaft())
            .ThenAsync(_ => setPreSaftUploaded.HandleAsync(migrationId, ct))
            .ThenAsync(_ => outbox.HandleAsync(migrationId, new SaftUploadRequested(), ct));
    }
}
