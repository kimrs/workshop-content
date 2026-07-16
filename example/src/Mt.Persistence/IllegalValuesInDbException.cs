namespace Mt.Persistence;

/// <summary>
/// Thrown when a persisted row cannot be reconstructed into a valid domain object.
/// Corrupt rows are bugs, not recoverable results (§4.9) — so this throws rather than
/// returning a <c>Result</c>.
/// </summary>
public sealed class IllegalValuesInDbException(string message) : Exception(message);
