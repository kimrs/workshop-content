using System.Text;
using Microsoft.Extensions.Logging;
using Mt.Domain;
using Mt.Domain.ExternalIds;
using Mt.Domain.Migrations;
using Mt.Domain.Stages.UploadsSaft;
using Mt.Results;
using IFetch = Mt.Domain.ExternalIds.IFetch;

namespace Mt.Source;

/// <summary>
/// Simulated Source SAF-T download (§7). Translates the migration id into Source's punch card
/// (spec 8) and returns an opaque file named after it — contents don't matter.
/// </summary>
public sealed partial class DownloadSaft(IFetch fetchExternalId, ILogger<DownloadSaft> logger) : IDownloadSaft
{
    public async Task<Result<SaftFile>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var punchCard = await fetchExternalId.HandleAsync(
            new IFetch.Request(migrationId, ExternalSystem.Source, IdType.PunchCard), ct);
        return punchCard.Then(id => Download(id.Value));
    }

    private Result<SaftFile> Download(IdValue punchCard)
    {
        Log.Downloaded(logger, punchCard.Value);
        var content = Encoding.UTF8.GetBytes($"<saft punchCard=\"{punchCard.Value}\" />");
        return SaftFile.Create($"{punchCard.Value}.xml", content);
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information,
            Message = "📥 [SIM] Downloaded SAF-T for {PunchCard}.")]
        public static partial void Downloaded(ILogger logger, string punchCard);
    }
}
