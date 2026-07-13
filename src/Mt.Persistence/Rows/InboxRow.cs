namespace Mt.Persistence.Rows;

/// <summary>
/// The inbox row (§8.4). Its composite primary key <c>(MigrationId, DomainEvent, Attempt)</c>
/// is the idempotency key — there is no <c>MessageId</c> (§2.2, §10). An attempt that aborted
/// (handler returned failures) is recorded here with <see cref="FailedAt"/> and
/// <see cref="FailureReason"/> — the durable trace a human looks at, and the marker that stops
/// redeliveries from re-running an attempt that already gave up (spec 12 D4).
/// </summary>
public sealed class InboxRow
{
    public long MigrationId { get; set; }

    public required string DomainEvent { get; set; }

    public int Attempt { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }

    public DateTimeOffset? FailedAt { get; set; }

    public string? FailureReason { get; set; }
}
