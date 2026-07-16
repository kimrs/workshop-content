namespace Mt.Domain.Steps.UnlocksSource;

/// <summary>Teardown fan-out: requests that Source be unlocked (§1.1).</summary>
public sealed record SourceUnlockRequested : DomainEvent
{
    public override string ToString() => nameof(SourceUnlockRequested);
}
