namespace Mt.Results;

/// <summary>A duplicate was found where uniqueness was expected.</summary>
public sealed record DuplicateFailure(string Message) : Failure(Message);
