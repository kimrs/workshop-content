using Mt.Results;

namespace Mt.Domain.Migrations;

/// <summary>
/// Persist the terminal <c>Unlocking → Completed</c> transition. Called by the unlock
/// fan-in within the same unit of work as the notification (§10 forbids notifying
/// before the terminal state is persisted).
/// </summary>
public interface ISetCompleted
{
    Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct);
}
