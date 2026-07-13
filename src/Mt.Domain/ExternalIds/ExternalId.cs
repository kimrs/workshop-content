using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.ExternalIds;

/// <summary>
/// One row of the id ledger: what a migration is called over in one external system
/// (spec 8). All fields are value objects; <see cref="Create"/> is the only constructor.
/// </summary>
public sealed record ExternalId
{
    private ExternalId(Id migrationId, ExternalSystem system, IdType type, IdValue value)
    {
        MigrationId = migrationId;
        System = system;
        Type = type;
        Value = value;
    }

    public Id MigrationId { get; }

    public ExternalSystem System { get; }

    public IdType Type { get; }

    public IdValue Value { get; }

    public static Result<ExternalId> Create(Id migrationId, ExternalSystem system, IdType type, IdValue value) =>
        new ExternalId(migrationId, system, type, value);
}
