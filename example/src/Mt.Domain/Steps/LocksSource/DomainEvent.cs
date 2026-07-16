namespace Mt.Domain.Steps.LocksSource;

/// <summary>Setup fan-out: requests that Source be locked (§1.1).</summary>
public sealed record SourceLockRequested : DomainEvent
{
    public override string ToString() => nameof(SourceLockRequested);
}
