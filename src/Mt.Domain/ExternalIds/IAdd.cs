using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.ExternalIds;

/// <summary>
/// Record an external id in the caller's unit of work (spec 8). Idempotent on the key
/// <c>(MigrationId, System, Type)</c>: replaying the same value succeeds, a different
/// value returns <see cref="ConflictingExternalIdFailure"/> — the outside world has
/// diverged from what we recorded.
/// </summary>
public interface IAdd
{
    Task<Result<ValueTuple>> HandleAsync(Request request, CancellationToken ct);

    public sealed record Request(Id MigrationId, ExternalSystem System, IdType Type, IdValue Value);
}
