using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Steps.LocksSource;

/// <summary>
/// Persist the <c>SourceLocked</c> fan-in flag; when it lands the last setup flag, the same
/// write performs <c>Created → Prepared</c> and reports it (§6.3, spec 9).
/// </summary>
public interface ISetSourceLocked
{
    Task<Result<Response>> HandleAsync(Id migrationId, CancellationToken ct);

    /// <summary>What the flag write produced, from the fan-in's point of view (spec 9).</summary>
    public closed record Response
    {
        /// <summary>This flag was the last one; the migration is now <see cref="Prepared"/>.</summary>
        public sealed record SetupComplete : Response;

        /// <summary>Another setup step is still pending (or the state has moved on).</summary>
        public sealed record SetupIncomplete : Response;
    }
}
