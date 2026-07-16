using Mt.Results;

namespace Mt.Domain.ExternalIds;

/// <summary>An id replay carried a different value than the one recorded (spec 8, <c>IAdd</c>).</summary>
public sealed record ConflictingExternalIdFailure(string Message) : Failure(Message);
