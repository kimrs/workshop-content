namespace Mt.Transport;

/// <summary>
/// The message envelope on the wire (§2.2). There is no <c>MessageId</c>; identity for dedup is
/// <c>(MigrationId, DomainEvent, Attempt)</c>. <c>required</c> members keep the mapping honest (§4.7).
/// </summary>
public sealed record MessageEnvelope
{
    public required long MigrationId { get; init; }

    public required string DomainEvent { get; init; }

    public required int Attempt { get; init; }

    public required string Payload { get; init; }

    public string? TraceParent { get; init; }
}
