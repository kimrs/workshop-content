using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Stages;

/// <summary>
/// A stage handler. The inbox looks handlers up by <see cref="EventType"/> (safely,
/// never <c>Single()</c>) and dispatches to <see cref="HandleAsync"/>. The envelope's
/// <c>Attempt</c> is infrastructure: <c>ExecuteOnce</c> consumes it for the inbox claim
/// and it goes no further (spec 7).
/// </summary>
public interface IHandleDomainEvent
{
    DomainEvent EventType { get; }

    Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct);
}
