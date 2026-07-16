using Microsoft.EntityFrameworkCore;
using Mt.Domain;
using Mt.Domain.Migrations;
using Mt.Domain.Steps;
using Mt.Domain.Steps.LocksSource;
using Mt.Persistence.Rows;
using Mt.Results;
using Xunit;

namespace Mt.Persistence.Tests;

/// <summary>
/// The retry arithmetic lives in <see cref="ScheduleEvent"/> (spec 7): current attempt is the
/// inbox max for <c>(MigrationId, DomainEvent)</c>, the budget comes from the caller.
/// </summary>
[Collection("postgres")]
public class ScheduleEventTests(PostgresFixture fixture)
{
    private async Task<Id> SeedAsync(int processedAttempts)
    {
        await using var db = fixture.CreateDbContext();
        var row = new MigrationRow
        {
            State = MigrationState.Created,
            OrganizationNumber = $"ORG-{Guid.NewGuid():N}",
        };
        db.Migrations.Add(row);
        await db.SaveChangesAsync();

        var eventName = new SourceLockRequested().ToMessageType();
        for (var attempt = 1; attempt <= processedAttempts; attempt++)
        {
            db.Inbox.Add(new InboxRow
            {
                MigrationId = row.Id,
                DomainEvent = eventName,
                Attempt = attempt,
                ReceivedAt = DateTimeOffset.UtcNow,
            });
        }

        await db.SaveChangesAsync();
        return Id.Create(row.Id).Unwrap();
    }

    [Fact]
    public async Task Below_budget_schedules_the_next_attempt()
    {
        var migrationId = await SeedAsync(processedAttempts: 1);
        await using var db = fixture.CreateDbContext();

        var result = await new ScheduleEvent(db).HandleAsync(migrationId, new SourceLockRequested(), 3, default);
        await db.SaveChangesAsync();

        Assert.True(result.IsCompleted(out var response, out _));
        var scheduled = Assert.IsType<IScheduleEvent.Response.Scheduled>(response);
        Assert.Equal(2, scheduled.Next.Value);

        await using var verify = fixture.CreateDbContext();
        var row = await verify.ScheduledEvents.SingleAsync(r => r.MigrationId == migrationId.Value);
        Assert.Equal(2, row.Attempt);
    }

    [Fact]
    public async Task Without_an_inbox_claim_the_contract_violation_is_a_typed_failure()
    {
        // Spec 12 D9: outside the inbox transaction there is no claim row — a diagnosable
        // failure, not an InvalidOperationException from MaxAsync.
        var migrationId = await SeedAsync(processedAttempts: 0);
        await using var db = fixture.CreateDbContext();

        var result = await new ScheduleEvent(db).HandleAsync(migrationId, new SourceLockRequested(), 3, default);

        Assert.True(result.IsFailed(out _, out var failures));
        Assert.IsType<NotFoundFailure>(failures[0]);
    }

    [Fact]
    public async Task At_budget_reports_exhausted_and_schedules_nothing()
    {
        var migrationId = await SeedAsync(processedAttempts: 3);
        await using var db = fixture.CreateDbContext();

        var result = await new ScheduleEvent(db).HandleAsync(migrationId, new SourceLockRequested(), 3, default);
        await db.SaveChangesAsync();

        Assert.True(result.IsCompleted(out var response, out _));
        Assert.IsType<IScheduleEvent.Response.Exhausted>(response);

        await using var verify = fixture.CreateDbContext();
        var rows = await verify.ScheduledEvents.CountAsync(r => r.MigrationId == migrationId.Value);
        Assert.Equal(0, rows);
    }
}
