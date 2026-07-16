namespace Mt.Persistence.Rows;

/// <summary>
/// The polling-table row for the <c>Postgres</c> transport only (§8.3, §11). Unused under
/// the default <c>InMemory</c> transport.
/// </summary>
public sealed class MessageRow
{
    public long Id { get; set; }

    public long MigrationId { get; set; }

    public required string DomainEvent { get; set; }

    public int Attempt { get; set; }

    public required string Payload { get; set; }

    public string? TraceParent { get; set; }

    public DateTimeOffset PublishedAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }
}
