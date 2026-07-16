using Mt.Results;

namespace Mt.Domain.Migrations;

/// <summary>
/// Persist the terminal <c>Cancelling → Cancelled</c> transition. Called by the unlock
/// fan-in within the same unit of work as the notification (§10).
/// </summary>
public interface ISetCancelled
{
    Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct);
}
