namespace Mt.Domain.Stages.LocksSource;

/// <summary>Retry budget for this stage (§6.5). Bound from configuration.</summary>
public sealed record Settings
{
    public int MaxAttempts { get; init; } = 3;
}
