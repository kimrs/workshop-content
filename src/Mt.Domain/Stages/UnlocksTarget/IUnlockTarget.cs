using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Stages.UnlocksTarget;

/// <summary>
/// Simulated Target: unlock the client (§6.3, §7). A fault at Target is an expected outcome the
/// stage retries (spec 11), not a failure. The adapter translates the migration id into
/// Target's holo-crystal (spec 8).
/// </summary>
public interface IUnlockTarget
{
    Task<Result<Response>> HandleAsync(Id migrationId, CancellationToken ct);

    /// <summary>What the unlock attempt produced, as the domain sees it (spec 11).</summary>
    public abstract record Response
    {
        /// <summary>Target unlocked the client.</summary>
        public sealed record Unlocked : Response;

        /// <summary>Target did not unlock this time; the stage decides whether to retry.</summary>
        public sealed record Faulted(string Reason) : Response;
    }
}
