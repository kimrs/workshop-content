namespace Mt.Domain.Stages.LocksTarget;

/// <summary>Setup fan-out: requests that Target be locked (§1.1).</summary>
public sealed record TargetLockRequested : DomainEvent
{
    public override string ToString() => nameof(TargetLockRequested);
}
