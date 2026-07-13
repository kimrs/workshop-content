using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Stages.Transforms;

/// <summary>
/// Persist the automatic <c>Created → Cancelling</c> transition when client data is
/// invalid (§1.2, §6.5). At this point both systems are locked, so both unlock flags
/// are seeded to <c>false</c> (both must be released).
/// </summary>
public interface ISetCancelling
{
    Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct);
}
