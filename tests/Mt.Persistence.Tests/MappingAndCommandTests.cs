using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Mt.Domain;
using Mt.Domain.Commands;
using Mt.Domain.ExternalIds;
using Mt.Persistence.Commands;
using Mt.Persistence.Migrations;
using Mt.Persistence.Rows;
using Mt.Results;
using Xunit;

namespace Mt.Persistence.Tests;

[Collection("postgres")]
public class MappingAndCommandTests(PostgresFixture fixture)
{
    private static IStart.Request Request(
        string organizationNumber,
        string? punchCard = null,
        string? holoCrystal = null,
        string? pigeonTag = null) => new(
        OrganizationNumber.Create(organizationNumber).Unwrap(),
        PunchCardNumber.Create(punchCard ?? $"PC-{Guid.NewGuid():N}").Unwrap(),
        HoloCrystalId.Create(holoCrystal ?? $"HOLO-{Guid.NewGuid():N}").Unwrap(),
        CarrierPigeonTag.Create(pigeonTag ?? $"PIGEON-{Guid.NewGuid():N}").Unwrap());

    private Start CreateStart(WorkshopDbContext db) =>
        new(db, new ExternalIds.Add(db), NullLogger<Start>.Instance);

    private sealed class RecordingNotify : INotifyCompletion
    {
        public List<INotifyCompletion.Request> Requests { get; } = [];

        public Task<Result<ValueTuple>> HandleAsync(INotifyCompletion.Request request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult<Result<ValueTuple>>(default(ValueTuple));
        }
    }

    [Fact]
    public void ToDomain_throws_IllegalValuesInDbException_on_corrupt_row()
    {
        var corrupt = new MigrationRow
        {
            Id = 0, // not a positive id — Id.Create fails
            State = MigrationState.Created,
            OrganizationNumber = "ORG-1",
        };

        Assert.Throws<IllegalValuesInDbException>(() => corrupt.ToDomain());
    }

    [Fact]
    public async Task Start_creates_migration_ids_and_three_setup_events_atomically()
    {
        await using var db = fixture.CreateDbContext();
        var request = Request($"ORG-{Guid.NewGuid():N}");

        var result = await CreateStart(db).HandleAsync(request, default);

        Assert.True(result.IsCompleted(out var id, out _));

        await using var verify = fixture.CreateDbContext();
        var setupEvents = await verify.Outbox
            .Where(o => o.MigrationId == id.Value)
            .Select(o => o.DomainEvent)
            .OrderBy(e => e)
            .ToListAsync();
        Assert.Equal(["ExportRequested", "SourceLockRequested", "TargetLockRequested"], setupEvents);

        var idRows = await verify.ExternalIds
            .Where(e => e.MigrationId == id.Value)
            .Select(e => e.System)
            .OrderBy(s => s)
            .ToListAsync();
        Assert.Equal(["Portal", "Source", "Target"], idRows);
    }

    [Fact]
    public async Task Start_rejects_a_second_active_migration_for_the_same_org()
    {
        var organizationNumber = $"ORG-{Guid.NewGuid():N}";
        await using var db = fixture.CreateDbContext();
        Assert.True((await CreateStart(db).HandleAsync(Request(organizationNumber), default)).IsCompleted(out _, out _));

        await using var db2 = fixture.CreateDbContext();
        var second = await CreateStart(db2).HandleAsync(Request(organizationNumber), default);

        Assert.True(second.IsFailed(out _, out var failures));
        Assert.IsType<DuplicateFailure>(failures[0]);
    }

    [Fact]
    public async Task Cancel_from_created_with_nothing_locked_finalizes_immediately()
    {
        var organizationNumber = $"ORG-{Guid.NewGuid():N}";
        await using var db = fixture.CreateDbContext();
        var started = await CreateStart(db).HandleAsync(Request(organizationNumber), default);
        Assert.True(started.IsCompleted(out var id, out _));

        var notify = new RecordingNotify();
        await using var db2 = fixture.CreateDbContext();
        var cancel = new Cancel(db2, new ExternalIds.Cancel(db2), notify, NullLogger<Cancel>.Instance);
        var cancelled = await cancel.HandleAsync(OrganizationNumber.Create(organizationNumber).Unwrap(), default);

        Assert.True(cancelled.IsCompleted(out _, out _));
        Assert.Single(notify.Requests);
        Assert.IsType<INotifyCompletion.Request.Cancelled>(notify.Requests[0]);

        await using var verify = fixture.CreateDbContext();
        var row = await verify.Migrations.SingleAsync(m => m.Id == id.Value);
        Assert.Equal(MigrationState.Cancelled, row.State);
        // The ledger is released so the ids can be reclaimed (spec 8)…
        Assert.Equal(3, await verify.ExternalIds.CountAsync(e => e.MigrationId == id.Value && e.IsCancelled));
        // …and no unlock events were fanned out: nothing was locked.
        var unlockEvents = await verify.Outbox.CountAsync(o =>
            o.MigrationId == id.Value
            && (o.DomainEvent == "SourceUnlockRequested" || o.DomainEvent == "TargetUnlockRequested"));
        Assert.Equal(0, unlockEvents);
    }

    [Fact]
    public async Task Start_rolls_back_entirely_when_an_external_id_is_already_claimed()
    {
        var punchCard = $"PC-{Guid.NewGuid():N}";
        await using var db = fixture.CreateDbContext();
        Assert.True(
            (await CreateStart(db).HandleAsync(Request($"ORG-{Guid.NewGuid():N}", punchCard), default))
                .IsCompleted(out _, out _));

        var conflictedOrg = $"ORG-{Guid.NewGuid():N}";
        await using var db2 = fixture.CreateDbContext();
        var second = await CreateStart(db2).HandleAsync(Request(conflictedOrg, punchCard), default);

        Assert.True(second.IsFailed(out _, out var failures));
        Assert.IsType<ExternalIdConflictFailure>(failures[0]);

        await using var verify = fixture.CreateDbContext();
        Assert.Equal(0, await verify.Migrations.CountAsync(m => m.OrganizationNumber == conflictedOrg));
    }
}
