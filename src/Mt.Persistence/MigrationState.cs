namespace Mt.Persistence;

/// <summary>
/// The migration's state as an <c>int</c> column (§11). Maps 1:1 to the aggregate's
/// sealed subclasses; the mapping lives in <c>Migrations.Mapping</c>. Numbered in
/// lifecycle order (renumbered by spec 9 D6, pre-production).
/// </summary>
public enum MigrationState
{
    Created = 0,
    Prepared = 1,
    Transformed = 2,
    PreSaftUploaded = 3,
    SaftUploaded = 4,
    Unlocking = 5,
    Completed = 6,
    Cancelling = 7,
    Cancelled = 8,
}
