namespace Mt.Target;

/// <summary>Failure configuration for the simulated Target, bound from the <c>Target</c> section (§7.1).</summary>
public sealed class TargetSettings
{
    public OperationFailure Lock { get; set; } = new();

    public OperationFailure Unlock { get; set; } = new();
}
