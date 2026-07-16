using Mt.Domain.Migrations;
using Mt.Domain.Steps;
using Mt.Results;

namespace Mt.Domain;

/// <summary>
/// Adds the next event to the outbox in the caller's unit of work (transactional
/// outbox, §8.1). A fan-out event always starts its step at the first attempt, so the
/// adapter stamps it (spec 7); it also fills in the payload (usually <c>"{}"</c>) and
/// the current W3C trace context. The domain only supplies the routing pair.
/// </summary>
public interface IAdd
{
    Task<Result<ValueTuple>> HandleAsync(Id migrationId, DomainEvent domainEvent, CancellationToken ct);
}
