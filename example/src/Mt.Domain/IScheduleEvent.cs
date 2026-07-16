using Mt.Domain.Migrations;
using Mt.Domain.Steps;
using Mt.Results;

namespace Mt.Domain;

/// <summary>
/// Schedules a bounded retry of an event, due at an adapter-chosen time (§11
/// ScheduledEvents). The adapter owns the attempt arithmetic: it derives the current
/// attempt from the inbox and either schedules the next one or reports the budget
/// spent (spec 7). The caller supplies only the policy — its <c>MaxAttempts</c>.
/// </summary>
public interface IScheduleEvent
{
    Task<Result<Response>> HandleAsync(Id migrationId, DomainEvent domainEvent, int maxAttempts, CancellationToken ct);

    public closed record Response
    {
        public sealed record Scheduled(Attempt Next) : Response;

        public sealed record Exhausted : Response;
    }
}
