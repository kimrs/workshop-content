using Mt.Results;

namespace Mt.Marble;

/// <summary>
/// A simulated Target operation was configured to fail (spec 2 §1). Not to be confused with
/// <see cref="OperationFailure"/>, which is the failure-injection *settings* for an operation.
/// Intentional near-duplicate of the Mt.DistinctComics counterpart (spec 1 §4.3).
/// </summary>
public sealed record OperationFailed(string Message) : Failure(Message);
