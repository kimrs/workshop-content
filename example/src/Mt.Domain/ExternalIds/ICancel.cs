using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.ExternalIds;

/// <summary>
/// Release every external id of a migration (spec 8): a bulk soft-delete in the caller's
/// transaction, run next to the terminal <c>Cancelled</c> transition. Re-cancelling is a
/// no-op, not an error. The filtered unique index then lets a new migration reclaim the
/// same ids.
/// </summary>
public interface ICancel
{
    Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct);
}
