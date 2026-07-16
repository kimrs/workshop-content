namespace Mt.Domain.Steps.LocksTarget;

/// <summary>Retry budget for this step (§6.5). Bound from configuration.</summary>
public sealed record Settings
{
    public int MaxAttempts { get; init; } = 3;
}
