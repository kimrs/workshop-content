using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Steps.UnlocksSource;

/// <summary>
/// Simulated Source: unlock the client (§6.3, §7). A fault at Source is an expected outcome the
/// step retries (spec 11), not a failure. The adapter translates the migration id into
/// Source's punch card (spec 8).
/// </summary>
public interface IUnlockSource
{
    Task<Result<Response>> HandleAsync(Id migrationId, CancellationToken ct);

    /// <summary>What the unlock attempt produced, as the domain sees it (spec 11).</summary>
    public union Response(Response.Unlocked, Response.Faulted)
    {
        /// <summary>Source unlocked the client.</summary>
        public sealed record Unlocked;

        /// <summary>Source did not unlock this time; the step decides whether to retry.</summary>
        public sealed record Faulted(string Reason);
    }
}
