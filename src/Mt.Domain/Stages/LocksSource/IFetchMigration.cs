using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Stages.LocksSource;

/// <summary>
/// Fetch the migration as this stage sees it (specs 4/5): what the work needs when the stage
/// should run, <see cref="Response.DoNotProceed"/> when there is nothing left to do here, or
/// <see cref="MigrationHasIncorrectState"/> for a state this stage must never see.
/// </summary>
public interface IFetchMigration
{
    Task<Result<Response>> HandleAsync(Id migrationId, CancellationToken ct);

    /// <summary>What the fetch found, from this stage's point of view (spec 5).</summary>
    public abstract record Response
    {
        /// <summary>The stage's work should run.</summary>
        public sealed record Proceed : Response;

        /// <summary>Cancelled, or Source already locked; the port has logged why.</summary>
        public sealed record DoNotProceed : Response;
    }
}
