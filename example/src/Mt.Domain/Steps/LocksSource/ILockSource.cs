using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Steps.LocksSource;

/// <summary>
/// Simulated Source: lock the client (§6.3, §7). A fault at Source is an expected outcome the
/// step retries (spec 11), not a failure. The adapter translates the migration id into
/// Source's punch card (spec 8).
/// </summary>
public interface ILockSource
{
    Task<Result<Response>> HandleAsync(Id migrationId, CancellationToken ct);

    /// <summary>What the lock attempt produced, as the domain sees it (spec 11).</summary>
    public closed record Response
    {
        /// <summary>Source locked the client.</summary>
        public sealed record Locked : Response;

        /// <summary>Source did not lock this time; the step decides whether to retry.</summary>
        public sealed record Faulted(string Reason) : Response;
    }
}
