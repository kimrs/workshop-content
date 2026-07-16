namespace Mt.Results;

/// <summary>A value was unexpectedly null (see <c>EnsureNotNull</c>).</summary>
public sealed record UnexpectedNullFailure(string Message) : Failure(Message);
