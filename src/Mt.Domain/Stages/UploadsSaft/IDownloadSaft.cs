using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Stages.UploadsSaft;

/// <summary>Simulated Source: download the exported SAF-T file (§6.3). The adapter translates the migration id into Source's punch card (spec 8).</summary>
public interface IDownloadSaft
{
    Task<Result<SaftFile>> HandleAsync(Id migrationId, CancellationToken ct);
}
