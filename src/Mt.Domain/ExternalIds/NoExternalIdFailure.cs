using Mt.Results;

namespace Mt.Domain.ExternalIds;

/// <summary>No active external id row for the requested <c>(MigrationId, System, Type)</c> (spec 8).</summary>
public sealed record NoExternalIdFailure(string Message) : Failure(Message);
