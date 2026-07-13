using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Stages.Transforms;

/// <summary>Persist the <c>Created → Transformed</c> transition (§6.5).</summary>
public interface ISetTransformed
{
    Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct);
}
