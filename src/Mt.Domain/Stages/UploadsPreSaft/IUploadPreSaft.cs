using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Stages.UploadsPreSaft;

/// <summary>Simulated Target: upload the pre-SAF-T work (§6.3). The adapter translates the migration id into Target's holo-crystal (spec 8).</summary>
public interface IUploadPreSaft
{
    Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct);
}
