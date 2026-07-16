namespace Mt.DistinctComics;

/// <summary>
/// Per-operation failure configuration (§7.1). Two modes: <see cref="FailUntilAttempt"/> fails
/// while the simulator's own call count is <c>&lt; N</c> then succeeds (transient failure +
/// retry — each handler attempt makes exactly one call, spec 7); <see cref="AlwaysFail"/>
/// fails every time (retry exhaustion). <see cref="Throw"/> makes the failure an
/// <c>OperationThrew</c> instead of a <c>OperationFailed</c>.
/// </summary>
public sealed class OperationFailure
{
    public int? FailUntilAttempt { get; set; }

    public bool AlwaysFail { get; set; }

    public bool Throw { get; set; }
}
