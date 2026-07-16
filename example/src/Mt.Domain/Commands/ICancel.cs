using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Commands;

/// <summary>
/// Requires an <see cref="ICancellable"/> state. Transitions to <c>Cancelling</c> and
/// writes the unlock events for whatever was actually locked, in one transaction (§9).
/// Fails with <see cref="MigrationHasIncorrectState"/> when not cancellable.
/// </summary>
public interface ICancel
{
    Task<Result<Id>> HandleAsync(OrganizationNumber organizationNumber, CancellationToken ct);
}
