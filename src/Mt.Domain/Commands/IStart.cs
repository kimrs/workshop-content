using Mt.Domain.ExternalIds;
using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Commands;

/// <summary>
/// Creates a migration in <c>Created</c>, records its external ids, and writes the three
/// setup events (<c>SourceLockRequested</c>, <c>TargetLockRequested</c>, <c>ExportRequested</c>)
/// to the outbox — all in one transaction (§9, spec 8). An id already claimed by another
/// active migration fails the whole command with <see cref="ExternalIdConflictFailure"/>.
/// </summary>
public interface IStart
{
    Task<Result<Id>> HandleAsync(Request request, CancellationToken ct);

    public sealed record Request(
        OrganizationNumber OrganizationNumber,
        PunchCardNumber PunchCardNumber,
        HoloCrystalId HoloCrystalId,
        CarrierPigeonTag CarrierPigeonTag);
}
