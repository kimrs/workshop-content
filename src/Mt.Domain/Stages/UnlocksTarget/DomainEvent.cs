namespace Mt.Domain.Stages.UnlocksTarget;

/// <summary>Teardown fan-out: requests that Target be unlocked (§1.1).</summary>
public sealed record TargetUnlockRequested : DomainEvent
{
    public override string ToString() => nameof(TargetUnlockRequested);
}
