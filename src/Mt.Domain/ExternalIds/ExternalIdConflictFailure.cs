using Mt.Results;

namespace Mt.Domain.ExternalIds;

/// <summary>
/// Another active migration already claims one of the ids supplied to Start (spec 8).
/// The API maps this to HTTP 409.
/// </summary>
public sealed record ExternalIdConflictFailure(string Message) : Failure(Message);
