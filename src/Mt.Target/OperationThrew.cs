using Mt.Results;

namespace Mt.Target;

/// <summary>
/// A simulated Target operation was configured to "throw" (<see cref="OperationFailure.Throw"/>) —
/// the failure a real edge adapter would produce from a caught exception (spec 10 D4).
/// Intentional near-duplicate of the Mt.Source counterpart (spec 1 §4.3).
/// </summary>
public sealed record OperationThrew(string Message) : Failure(Message);
