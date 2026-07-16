using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Steps.UploadsSaft;

/// <summary>Simulated Target: upload the SAF-T file (§6.3). The adapter translates the migration id into Target's holo-crystal (spec 8).</summary>
public interface IUploadSaft
{
    Task<Result<ValueTuple>> HandleAsync(Id migrationId, SaftFile saftFile, CancellationToken ct);
}
