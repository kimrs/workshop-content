using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mt.Domain.ExternalIds;
using Mt.Domain.Migrations;
using Mt.Domain.Steps.UnlocksSource;
using Mt.Results;
using IFetch = Mt.Domain.ExternalIds.IFetch;

namespace Mt.DistinctComics;

/// <summary>
/// Simulated Source unlock (§7). Translates the migration id into Source's punch card (spec 8)
/// and reports a simulator failure as a <c>Faulted</c> outcome (spec 11) — only the id
/// resolution can fail the result.
/// </summary>
public sealed class UnlockSource(
    IOptions<SourceSettings> settings,
    IFetch fetchExternalId,
    Simulator simulator,
    ILogger<UnlockSource> logger) : IUnlockSource
{
    public async Task<Result<IUnlockSource.Response>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var punchCard = await fetchExternalId.HandleAsync(
            new IFetch.Request(migrationId, ExternalSystem.Source, IdType.PunchCard), ct);
        return punchCard.Then(id => simulator.Run(
                settings.Value.Unlock, "🔓 [SIM] Source unlocked", "Source unlock", id.Value, logger)
            .Match(
                completed: IUnlockSource.Response (_) => new IUnlockSource.Response.Unlocked(),
                failed: failures => new IUnlockSource.Response.Faulted(failures[0].Message)));
    }
}
