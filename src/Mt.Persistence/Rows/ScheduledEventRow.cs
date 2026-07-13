namespace Mt.Persistence.Rows;

/// <summary>
/// A retry scheduled by <c>IScheduleEvent</c> (§11). The outbox worker promotes due rows
/// (<c>ScheduledAt &lt;= now</c>, not yet processed) into the outbox.
/// </summary>
public sealed class ScheduledEventRow
{
    public long Id { get; set; }

    public long MigrationId { get; set; }

    public required string DomainEvent { get; set; }

    public int Attempt { get; set; }

    public required string Payload { get; set; }

    /// <summary>The originating message's trace, carried through promotion so retries stay correlated (spec 12 D8).</summary>
    public string? TraceParent { get; set; }

    public DateTimeOffset ScheduledAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }
}
