using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mt.Domain.ExternalIds;
using Mt.Domain.Migrations;
using Mt.Domain.Steps.LocksTarget;
using Mt.Results;
using IFetch = Mt.Domain.ExternalIds.IFetch;

namespace Mt.Marble;

/// <summary>
/// Simulated Target lock (§7). Translates the migration id into Target's holo-crystal (spec 8)
/// and reports a simulator failure as a <c>Faulted</c> outcome (spec 11) — only the id
/// resolution can fail the result.
/// </summary>
public sealed class LockTarget(
    IOptions<TargetSettings> settings,
    IFetch fetchExternalId,
    Simulator simulator,
    ILogger<LockTarget> logger) : ILockTarget
{
    public async Task<Result<ILockTarget.Response>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var holoCrystal = await fetchExternalId.HandleAsync(
            new IFetch.Request(migrationId, ExternalSystem.Target, IdType.HoloCrystal), ct);
        return holoCrystal.Then(id => simulator.Run(
                settings.Value.Lock, "🔒 [SIM] Target locked", "Target lock", id.Value, logger)
            .Match(
                completed: ILockTarget.Response (_) => new ILockTarget.Response.Locked(),
                failed: failures => new ILockTarget.Response.Faulted(failures[0].Message)));
    }
}
