using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Steps.UnlocksSource;

/// <summary>
/// Fetch the migration as this step sees it (specs 4/5): what the work needs when the step
/// should run — teardown covers both <see cref="Unlocking"/> (approve) and
/// <see cref="Cancelling"/> (cancel) — <see cref="Response.DoNotProceed"/> when there is
/// nothing left to do here, or <see cref="MigrationHasIncorrectState"/> for a state this
/// step must never see.
/// </summary>
public interface IFetchMigration
{
    Task<Result<Response>> HandleAsync(Id migrationId, CancellationToken ct);

    /// <summary>
    /// What the fetch found, from this step's point of view (spec 5). A union: the case
    /// records share no base type; <c>Response</c> is a struct that holds exactly one of
    /// them, and switches over it are checked for exhaustiveness.
    /// </summary>
    public union Response(Response.Proceed, Response.DoNotProceed)
    {
        /// <summary>The step's work should run.</summary>
        public sealed record Proceed;

        /// <summary>Already finalized, or Source already unlocked; the port has logged why.</summary>
        public sealed record DoNotProceed;
    }
}
