using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Stages.LocksTarget;

/// <summary>
/// Simulated Target: lock the client (§6.3, §7). A fault at Target is an expected outcome the
/// stage retries (spec 11), not a failure. The adapter translates the migration id into
/// Target's holo-crystal (spec 8).
/// </summary>
public interface ILockTarget
{
    Task<Result<Response>> HandleAsync(Id migrationId, CancellationToken ct);

    /// <summary>What the lock attempt produced, as the domain sees it (spec 11).</summary>
    public abstract record Response
    {
        /// <summary>Target locked the client.</summary>
        public sealed record Locked : Response;

        /// <summary>Target did not lock this time; the stage decides whether to retry.</summary>
        public sealed record Faulted(string Reason) : Response;
    }
}
