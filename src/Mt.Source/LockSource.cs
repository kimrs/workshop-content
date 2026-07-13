using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mt.Domain.ExternalIds;
using Mt.Domain.Migrations;
using Mt.Domain.Stages.LocksSource;
using Mt.Results;
using IFetch = Mt.Domain.ExternalIds.IFetch;

namespace Mt.Source;

/// <summary>
/// Simulated Source lock (§7). Translates the migration id into Source's punch card (spec 8)
/// and reports a simulator failure as a <c>Faulted</c> outcome (spec 11) — only the id
/// resolution can fail the result.
/// </summary>
public sealed class LockSource(
    IOptions<SourceSettings> settings,
    IFetch fetchExternalId,
    Simulator simulator,
    ILogger<LockSource> logger) : ILockSource
{
    public async Task<Result<ILockSource.Response>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var punchCard = await fetchExternalId.HandleAsync(
            new IFetch.Request(migrationId, ExternalSystem.Source, IdType.PunchCard), ct);
        return punchCard.Then(id => simulator.Run(
                settings.Value.Lock, "🔒 [SIM] Source locked", "Source lock", id.Value, logger)
            .Match(
                completed: ILockSource.Response (_) => new ILockSource.Response.Locked(),
                failed: failures => new ILockSource.Response.Faulted(failures[0].Message)));
    }
}
