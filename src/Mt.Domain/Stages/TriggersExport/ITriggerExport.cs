using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Stages.TriggersExport;

/// <summary>
/// Simulated Source: trigger the SAF-T export (§6.3, §7). A fault at Source is an expected
/// outcome the stage retries (spec 11), not a failure. The adapter translates the migration id
/// into Source's punch card (spec 8).
/// </summary>
public interface ITriggerExport
{
    Task<Result<Response>> HandleAsync(Id migrationId, CancellationToken ct);

    /// <summary>What the trigger attempt produced, as the domain sees it (spec 11).</summary>
    public abstract record Response
    {
        /// <summary>Source started the export.</summary>
        public sealed record Triggered : Response;

        /// <summary>Source did not start the export this time; the stage decides whether to retry.</summary>
        public sealed record Faulted(string Reason) : Response;
    }
}
