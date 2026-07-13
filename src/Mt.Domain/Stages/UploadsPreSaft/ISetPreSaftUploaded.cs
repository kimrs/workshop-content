using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Stages.UploadsPreSaft;

/// <summary>Persist the <c>Transformed → PreSaftUploaded</c> transition (§6.5).</summary>
public interface ISetPreSaftUploaded
{
    Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct);
}
