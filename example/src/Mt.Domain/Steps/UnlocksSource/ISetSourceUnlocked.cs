using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Steps.UnlocksSource;

/// <summary>
/// Persist the <c>SourceUnlocked</c> fan-in flag and report whether that finished the
/// teardown, and on which path (§6.3, spec 9).
/// </summary>
public interface ISetSourceUnlocked
{
    Task<Result<Response>> HandleAsync(Id migrationId, CancellationToken ct);

    /// <summary>What the flag write produced, from the fan-in's point of view (spec 9).</summary>
    public union Response(Response.Complete, Response.Cancel, Response.TeardownIncomplete)
    {
        /// <summary>Both systems unlocked on the approve path; finalize to <see cref="Completed"/>.</summary>
        public sealed record Complete(Unlocking Migration);

        /// <summary>Both systems unlocked on the cancel path; finalize to <see cref="Cancelled"/>.</summary>
        public sealed record Cancel(Cancelling Migration);

        /// <summary>The other unlock is still pending (or the state has moved on).</summary>
        public sealed record TeardownIncomplete;
    }
}
