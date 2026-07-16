namespace Mt.Persistence.Rows;

/// <summary>The <c>Migrations</c> table row (§11): state, org number, fan-in flags.</summary>
public sealed class MigrationRow
{
    public long Id { get; set; }

    public MigrationState State { get; set; }

    public required string OrganizationNumber { get; set; }

    public bool SourceLocked { get; set; }

    public bool TargetLocked { get; set; }

    public bool ExportTriggered { get; set; }

    public bool SourceUnlocked { get; set; }

    public bool TargetUnlocked { get; set; }
}
