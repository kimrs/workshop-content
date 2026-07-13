using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Stages.UnlocksTarget;

/// <summary>
/// Persist the <c>TargetUnlocked</c> fan-in flag and report whether that finished the
/// teardown, and on which path (§6.3, spec 9).
/// </summary>
public interface ISetTargetUnlocked
{
    Task<Result<Response>> HandleAsync(Id migrationId, CancellationToken ct);

    /// <summary>What the flag write produced, from the fan-in's point of view (spec 9).</summary>
    public abstract record Response
    {
        /// <summary>Both systems unlocked on the approve path; finalize to <see cref="Completed"/>.</summary>
        public sealed record Complete(Unlocking Migration) : Response;

        /// <summary>Both systems unlocked on the cancel path; finalize to <see cref="Cancelled"/>.</summary>
        public sealed record Cancel(Cancelling Migration) : Response;

        /// <summary>The other unlock is still pending (or the state has moved on).</summary>
        public sealed record TeardownIncomplete : Response;
    }
}
