namespace Mt.Persistence.Rows;

/// <summary>The transactional-outbox row (§8.1). Carries the message envelope plus lifecycle timestamps.</summary>
public sealed class OutboxRow
{
    public long Id { get; set; }

    public long MigrationId { get; set; }

    public required string DomainEvent { get; set; }

    public int Attempt { get; set; }

    public required string Payload { get; set; }

    public string? TraceParent { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public DateTimeOffset? FailedAt { get; set; }

    public string? FailureReason { get; set; }
}
