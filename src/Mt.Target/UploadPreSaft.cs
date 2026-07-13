using Microsoft.Extensions.Logging;
using Mt.Domain.ExternalIds;
using Mt.Domain.Migrations;
using Mt.Domain.Stages.UploadsPreSaft;
using Mt.Results;
using IFetch = Mt.Domain.ExternalIds.IFetch;

namespace Mt.Target;

/// <summary>
/// Simulated Target pre-SAF-T upload (§7). Translates the migration id into Target's
/// holo-crystal (spec 8). Not retryable, so it simply succeeds.
/// </summary>
public sealed partial class UploadPreSaft(IFetch fetchExternalId, ILogger<UploadPreSaft> logger) : IUploadPreSaft
{
    public async Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var holoCrystal = await fetchExternalId.HandleAsync(
            new IFetch.Request(migrationId, ExternalSystem.Target, IdType.HoloCrystal), ct);
        return holoCrystal.Then(id => Upload(id.Value));
    }

    private Result<ValueTuple> Upload(IdValue holoCrystal)
    {
        Log.Uploaded(logger, holoCrystal.Value);
        return default(ValueTuple);
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information,
            Message = "📦 [SIM] Uploaded pre-SAF-T work into {HoloCrystal}.")]
        public static partial void Uploaded(ILogger logger, string holoCrystal);
    }
}
