using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Steps.Transforms;

/// <summary>
/// Fetch the migration as this step sees it (specs 4/5): the <see cref="Prepared"/> state to
/// work on, <see cref="Response.DoNotProceed"/> when the migration was cancelled, or
/// <see cref="MigrationHasIncorrectState"/> for a state this step must never see.
/// </summary>
public interface IFetchMigration
{
    Task<Result<Response>> HandleAsync(Id migrationId, CancellationToken ct);

    /// <summary>What the fetch found, from this step's point of view (spec 5).</summary>
    public abstract record Response
    {
        /// <summary>The migration is in the state this step works on.</summary>
        public sealed record Proceed(Prepared Migration) : Response;

        /// <summary>The migration was cancelled; the port has logged why.</summary>
        public sealed record DoNotProceed : Response;
    }
}
