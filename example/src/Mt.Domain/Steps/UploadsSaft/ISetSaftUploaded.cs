using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Steps.UploadsSaft;

/// <summary>Persist the <c>PreSaftUploaded → SaftUploaded</c> transition (§6.5).</summary>
public interface ISetSaftUploaded
{
    Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct);
}
