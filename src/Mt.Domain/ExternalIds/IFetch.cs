using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.ExternalIds;

/// <summary>
/// Resolve what one external system calls a migration (spec 8). Reads active rows only;
/// not-found is <see cref="NoExternalIdFailure"/>, never <c>null</c>. This is the port every
/// simulated adapter uses to translate <c>MigrationId</c> into its own id.
/// </summary>
public interface IFetch
{
    Task<Result<ExternalId>> HandleAsync(Request request, CancellationToken ct);

    public sealed record Request(Id MigrationId, ExternalSystem System, IdType Type);
}
