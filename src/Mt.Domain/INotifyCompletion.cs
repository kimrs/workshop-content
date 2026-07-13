using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain;

/// <summary>
/// The final "done" notification (§6.3), surfaced to users by the portal frontend.
/// Called by the unlock fan-in within the terminal transition's unit of work.
/// </summary>
public interface INotifyCompletion
{
    Task<Result<ValueTuple>> HandleAsync(Request request, CancellationToken ct);

    /// <summary>
    /// The completion notification message (spec 2 §3): how a migration finished, as one
    /// subtype per outcome instead of an enum. Nested in the port per spec 5; <c>Cancelled</c>
    /// would otherwise also collide with the migration state of the same name.
    /// </summary>
    public abstract record Request(Id MigrationId)
    {
        /// <summary>The migration ran to the end and the client now lives in Target.</summary>
        public sealed record Migrated(Id MigrationId) : Request(MigrationId);

        /// <summary>The migration was cancelled and both systems are unlocked again.</summary>
        public sealed record Cancelled(Id MigrationId) : Request(MigrationId);
    }
}
