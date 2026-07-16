using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Commands;

/// <summary>
/// Requires <c>SaftUploaded</c>. Transitions to <c>Unlocking</c> and writes
/// <c>SourceUnlockRequested</c> + <c>TargetUnlockRequested</c> (at <see cref="Attempt.First"/>)
/// to the outbox in one transaction (§9). Fails with <see cref="MigrationHasIncorrectState"/>
/// when not in <c>SaftUploaded</c>.
/// </summary>
public interface IApprove
{
    Task<Result<Id>> HandleAsync(OrganizationNumber organizationNumber, CancellationToken ct);
}
