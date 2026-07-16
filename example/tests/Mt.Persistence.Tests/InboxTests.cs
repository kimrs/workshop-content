using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Mt.Domain;
using Mt.Domain.Migrations;
using Mt.Domain.Steps;
using Mt.Domain.Steps.LocksSource;
using Mt.Persistence;
using Mt.Persistence.Rows;
using Mt.Results;
using Xunit;

namespace Mt.Persistence.Tests;

[Collection("postgres")]
public class InboxTests(PostgresFixture fixture)
{
    private sealed class CountingHandler : IHandleDomainEvent
    {
        private int _count;

        public int Count => _count;

        public DomainEvent EventType { get; } = new SourceLockRequested();

        public Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct)
        {
            Interlocked.Increment(ref _count);
            return Task.FromResult<Result<ValueTuple>>(default(ValueTuple));
        }
    }

    private sealed class AbortingHandler : IHandleDomainEvent
    {
        private int _count;

        public int Count => _count;

        public DomainEvent EventType { get; } = new SourceLockRequested();

        public Task<Result<ValueTuple>> HandleAsync(Id migrationId, CancellationToken ct)
        {
            Interlocked.Increment(ref _count);
            return Task.FromResult<Result<ValueTuple>>(new ValidationFailure("the world is corrupt"));
        }
    }

    private async Task<Id> SeedMigrationAsync()
    {
        await using var db = fixture.CreateDbContext();
        var row = new MigrationRow
        {
            State = MigrationState.Created,
            OrganizationNumber = $"ORG-{Guid.NewGuid():N}",
        };
        db.Migrations.Add(row);
        await db.SaveChangesAsync();
        return Id.Create(row.Id).Unwrap();
    }

    [Fact]
    public async Task Duplicate_same_attempt_runs_the_handler_at_most_once()
    {
        var migrationId = await SeedMigrationAsync();
        var handler = new CountingHandler();
        await using var db = fixture.CreateDbContext();
        var executeOnce = new ExecuteOnce(db, [handler], NullLogger<ExecuteOnce>.Instance);

        var first = await executeOnce.ExecuteOnceAsync(migrationId, new SourceLockRequested(), Attempt.First, default);
        var second = await executeOnce.ExecuteOnceAsync(migrationId, new SourceLockRequested(), Attempt.First, default);

        Assert.True(first.IsCompleted(out _, out _));
        Assert.True(second.IsCompleted(out _, out _));
        Assert.Equal(1, handler.Count);

        await using var verify = fixture.CreateDbContext();
        var inboxRows = await verify.Inbox.CountAsync(i => i.MigrationId == migrationId.Value);
        Assert.Equal(1, inboxRows);
    }

    [Fact]
    public async Task Aborted_handler_records_the_failure_on_the_inbox_row()
    {
        var migrationId = await SeedMigrationAsync();
        await using var db = fixture.CreateDbContext();
        var executeOnce = new ExecuteOnce(db, [new AbortingHandler()], NullLogger<ExecuteOnce>.Instance);

        var result = await executeOnce.ExecuteOnceAsync(migrationId, new SourceLockRequested(), Attempt.First, default);

        Assert.True(result.IsFailed(out _, out var failures));
        Assert.Equal("the world is corrupt", failures[0].Message);

        await using var verify = fixture.CreateDbContext();
        var row = await verify.Inbox.SingleAsync(i => i.MigrationId == migrationId.Value);
        Assert.NotNull(row.FailedAt);
        Assert.Equal("the world is corrupt", row.FailureReason);
    }

    [Fact]
    public async Task Redelivery_of_an_aborted_attempt_does_not_rerun_the_handler()
    {
        var migrationId = await SeedMigrationAsync();
        var handler = new AbortingHandler();
        await using var db = fixture.CreateDbContext();
        var executeOnce = new ExecuteOnce(db, [handler], NullLogger<ExecuteOnce>.Instance);

        var first = await executeOnce.ExecuteOnceAsync(migrationId, new SourceLockRequested(), Attempt.First, default);
        var redelivered = await executeOnce.ExecuteOnceAsync(migrationId, new SourceLockRequested(), Attempt.First, default);

        Assert.True(first.IsFailed(out _, out _));
        // The abort stands recorded; the redelivery is acknowledged without re-running anything.
        Assert.True(redelivered.IsCompleted(out _, out _));
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task Concurrent_duplicate_loses_the_unique_violation_race_gracefully()
    {
        var migrationId = await SeedMigrationAsync();
        var handler = new CountingHandler();
        await using var db1 = fixture.CreateDbContext();
        await using var db2 = fixture.CreateDbContext();
        var executeOnce1 = new ExecuteOnce(db1, [handler], NullLogger<ExecuteOnce>.Instance);
        var executeOnce2 = new ExecuteOnce(db2, [handler], NullLogger<ExecuteOnce>.Instance);

        var results = await Task.WhenAll(
            executeOnce1.ExecuteOnceAsync(migrationId, new SourceLockRequested(), Attempt.First, default),
            executeOnce2.ExecuteOnceAsync(migrationId, new SourceLockRequested(), Attempt.First, default));

        Assert.All(results, r => Assert.True(r.IsCompleted(out _, out _)));
        Assert.Equal(1, handler.Count);
    }
}
