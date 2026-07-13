using Microsoft.Extensions.Logging;
using Mt.Domain;
using Mt.Domain.ExternalIds;
using Mt.Domain.Migrations;
using Mt.Domain.Stages.UploadsSaft;
using Mt.Results;
using IFetch = Mt.Domain.ExternalIds.IFetch;

namespace Mt.Target;

/// <summary>
/// Simulated Target SAF-T upload (§7). Translates the migration id into Target's
/// holo-crystal (spec 8). Not retryable, so it simply succeeds.
/// </summary>
public sealed partial class UploadSaft(IFetch fetchExternalId, ILogger<UploadSaft> logger) : IUploadSaft
{
    public async Task<Result<ValueTuple>> HandleAsync(Id migrationId, SaftFile saftFile, CancellationToken ct)
    {
        var holoCrystal = await fetchExternalId.HandleAsync(
            new IFetch.Request(migrationId, ExternalSystem.Target, IdType.HoloCrystal), ct);
        return holoCrystal.Then(id => Upload(saftFile, id.Value));
    }

    private Result<ValueTuple> Upload(SaftFile saftFile, IdValue holoCrystal)
    {
        Log.Uploaded(logger, saftFile.FileName, saftFile.Content.Length, holoCrystal.Value);
        return default(ValueTuple);
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information,
            Message = "📤 [SIM] Uploaded SAF-T file {FileName} ({Bytes} bytes) into {HoloCrystal}.")]
        public static partial void Uploaded(ILogger logger, string fileName, int bytes, string holoCrystal);
    }
}
