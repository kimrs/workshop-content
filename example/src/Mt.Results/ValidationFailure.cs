namespace Mt.Results;

/// <summary>A value failed a validation rule (e.g. a <c>Create</c> guard).</summary>
public sealed record ValidationFailure(string Message) : Failure(Message);
