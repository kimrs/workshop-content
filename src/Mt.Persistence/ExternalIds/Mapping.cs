using Mt.Domain.ExternalIds;
using Mt.Domain.Migrations;
using Mt.Persistence.Rows;
using Mt.Results;

namespace Mt.Persistence.ExternalIds;

/// <summary>
/// Row ↔ domain mapping for external ids (spec 8). Rehydration returns failures for corrupt
/// rows (the pattern's contract — this deviates from <c>LoadMigrationAsync</c>'s throw).
/// The <c>ToExternalId</c> extensions pin each Start input to its <c>(System, IdType)</c>:
/// that mapping is a storage decision, so it lives here, not in the domain.
/// </summary>
internal static class Mapping
{
    extension(ExternalIdRow row)
    {
        public Result<ExternalId> ToDomain() =>
            Id.Create(row.MigrationId)
                .Then(migrationId => ExternalSystem.Create(row.System)
                    .Then(system => IdType.Create(row.Name)
                        .Then(type => IdValue.Create(row.Value)
                            .Then(value => ExternalId.Create(migrationId, system, type, value)))));
    }

    extension(PunchCardNumber punchCard)
    {
        public Result<ExternalId> ToExternalId(Id migrationId) =>
            IdValue.Create(punchCard.Value)
                .Then(value => ExternalId.Create(migrationId, ExternalSystem.Source, IdType.PunchCard, value));
    }

    extension(HoloCrystalId holoCrystal)
    {
        public Result<ExternalId> ToExternalId(Id migrationId) =>
            IdValue.Create(holoCrystal.Value)
                .Then(value => ExternalId.Create(migrationId, ExternalSystem.Target, IdType.HoloCrystal, value));
    }

    extension(CarrierPigeonTag pigeonTag)
    {
        public Result<ExternalId> ToExternalId(Id migrationId) =>
            IdValue.Create(pigeonTag.Value)
                .Then(value => ExternalId.Create(migrationId, ExternalSystem.Portal, IdType.CarrierPigeon, value));
    }

    extension(ExternalId externalId)
    {
        public ExternalIdRow ToRow() => new()
        {
            MigrationId = externalId.MigrationId.Value,
            System = externalId.System.Value,
            Name = externalId.Type.Value,
            Value = externalId.Value.Value,
        };
    }
}
