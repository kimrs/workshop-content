using Mt.Results;

namespace Mt.Source;

/// <summary>
/// A simulated Source operation was configured to fail (spec 2 §1). Not to be confused with
/// <see cref="OperationFailure"/>, which is the failure-injection *settings* for an operation.
/// Intentional near-duplicate of the Mt.Target counterpart (spec 1 §4.3).
/// </summary>
public sealed record OperationFailed(string Message) : Failure(Message);
