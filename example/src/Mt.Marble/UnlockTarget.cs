using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mt.Domain.ExternalIds;
using Mt.Domain.Migrations;
using Mt.Domain.Steps.UnlocksTarget;
using Mt.Results;
using IFetch = Mt.Domain.ExternalIds.IFetch;

namespace Mt.Marble;

/// <summary>
/// Simulated Target unlock (§7). Translates the migration id into Target's holo-crystal (spec 8)
/// and reports a simulator failure as a <c>Faulted</c> outcome (spec 11) — only the id
/// resolution can fail the result.
/// </summary>
public sealed class UnlockTarget(
    IOptions<TargetSettings> settings,
    IFetch fetchExternalId,
    Simulator simulator,
    ILogger<UnlockTarget> logger) : IUnlockTarget
{
    public async Task<Result<IUnlockTarget.Response>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var holoCrystal = await fetchExternalId.HandleAsync(
            new IFetch.Request(migrationId, ExternalSystem.Target, IdType.HoloCrystal), ct);
        return holoCrystal.Then(id => simulator.Run(
                settings.Value.Unlock, "🔓 [SIM] Target unlocked", "Target unlock", id.Value, logger)
            .Match(
                completed: IUnlockTarget.Response (_) => new IUnlockTarget.Response.Unlocked(),
                failed: failures => new IUnlockTarget.Response.Faulted(failures[0].Message)));
    }
}
