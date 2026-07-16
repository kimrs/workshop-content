namespace Mt.Marble;

/// <summary>
/// Per-operation failure configuration for Target (§7.1). Deliberately a near-duplicate of the
/// Source copy — the two simulators are independent adapters (§4.3).
/// </summary>
public sealed class OperationFailure
{
    public int? FailUntilAttempt { get; set; }

    public bool AlwaysFail { get; set; }

    public bool Throw { get; set; }
}
