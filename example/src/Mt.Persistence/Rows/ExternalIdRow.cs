namespace Mt.Persistence.Rows;

/// <summary>
/// The <c>ExternalIds</c> table row (spec 8): what one external system calls a migration.
/// Cancellation is a soft delete — the filtered unique index ignores cancelled rows so a
/// new migration can reclaim the same id.
/// </summary>
public sealed class ExternalIdRow
{
    public long MigrationId { get; set; }

    public required string System { get; set; }

    public required string Name { get; set; }

    public required string Value { get; set; }

    public bool IsCancelled { get; set; }
}
