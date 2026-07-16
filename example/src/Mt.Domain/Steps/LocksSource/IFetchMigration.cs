using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Steps.LocksSource;

/// <summary>
/// Fetch the migration as this step sees it (specs 4/5): what the work needs when the step
/// should run, <see cref="Response.DoNotProceed"/> when there is nothing left to do here, or
/// <see cref="MigrationHasIncorrectState"/> for a state this step must never see.
/// </summary>
public interface IFetchMigration
{
    Task<Result<Response>> HandleAsync(Id migrationId, CancellationToken ct);

    /// <summary>
    /// What the fetch found, from this step's point of view (spec 5). The hierarchy is
    /// <c>closed</c>: the compiler knows these nested records are the only cases, so
    /// switches over it are checked for exhaustiveness and need no discard arm.
    /// </summary>
    public closed record Response
    {
        /// <summary>The step's work should run.</summary>
        public sealed record Proceed : Response;

        /// <summary>Cancelled, or Source already locked; the port has logged why.</summary>
        public sealed record DoNotProceed : Response;
    }
}
