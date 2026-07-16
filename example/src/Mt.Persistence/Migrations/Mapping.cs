using Mt.Domain;
using Mt.Domain.Migrations;
using Mt.Persistence.Rows;
using Mt.Results;

namespace Mt.Persistence.Migrations;

/// <summary>
/// Reconstructs a domain <see cref="Migration"/> from a <see cref="MigrationRow"/> (§4.7 manual
/// mapping). Throws <see cref="IllegalValuesInDbException"/> when a value object's <c>Create</c>
/// fails — corrupt rows are bugs, not recoverable results (§4.9).
/// </summary>
public static class Mapping
{
    extension(MigrationRow row)
    {
        public Migration ToDomain()
        {
            var id = ToId(row.Id);
            var organizationNumber = ToOrganizationNumber(row.OrganizationNumber);

            return row.State switch
            {
                MigrationState.Created =>
                    new Created(id, organizationNumber, row.SourceLocked, row.TargetLocked, row.ExportTriggered),
                MigrationState.Prepared => new Prepared(id, organizationNumber),
                MigrationState.Transformed => new Transformed(id, organizationNumber),
                MigrationState.PreSaftUploaded => new PreSaftUploaded(id, organizationNumber),
                MigrationState.SaftUploaded => new SaftUploaded(id, organizationNumber),
                MigrationState.Unlocking =>
                    new Unlocking(id, organizationNumber, row.SourceUnlocked, row.TargetUnlocked),
                MigrationState.Completed => new Completed(id, organizationNumber),
                MigrationState.Cancelling =>
                    new Cancelling(id, organizationNumber, row.SourceUnlocked, row.TargetUnlocked),
                MigrationState.Cancelled => new Cancelled(id, organizationNumber),
                _ => throw new IllegalValuesInDbException(
                    $"Unknown migration state {row.State} for migration {row.Id}."),
            };
        }
    }

    private static Id ToId(long value) =>
        Unwrap(Id.Create(value), $"Illegal migration id {value} in database.");

    private static OrganizationNumber ToOrganizationNumber(string value) =>
        Unwrap(OrganizationNumber.Create(value), $"Illegal organization number '{value}' in database.");

    private static T Unwrap<T>(Result<T> result, string message) =>
        result.IsCompleted(out var value, out _)
            ? value
            : throw new IllegalValuesInDbException(message);
}
