using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mt.Domain.ExternalIds;
using Mt.Domain.Migrations;
using Mt.Domain.Steps.TriggersExport;
using Mt.Results;
using IFetch = Mt.Domain.ExternalIds.IFetch;

namespace Mt.DistinctComics;

/// <summary>
/// Simulated Source SAF-T export trigger (§7). Translates the migration id into Source's punch
/// card (spec 8) and reports a simulator failure as a <c>Faulted</c> outcome (spec 11) — only
/// the id resolution can fail the result.
/// </summary>
public sealed class TriggerExport(
    IOptions<SourceSettings> settings,
    IFetch fetchExternalId,
    Simulator simulator,
    ILogger<TriggerExport> logger) : ITriggerExport
{
    public async Task<Result<ITriggerExport.Response>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var punchCard = await fetchExternalId.HandleAsync(
            new IFetch.Request(migrationId, ExternalSystem.Source, IdType.PunchCard), ct);
        return punchCard.Then(id => simulator.Run(
                settings.Value.TriggerExport, "📤 [SIM] SAF-T export triggered", "Export trigger", id.Value, logger)
            .Match(
                completed: ITriggerExport.Response (_) => new ITriggerExport.Response.Triggered(),
                failed: failures => new ITriggerExport.Response.Faulted(failures[0].Message)));
    }
}
