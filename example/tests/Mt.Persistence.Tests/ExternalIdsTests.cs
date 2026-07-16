using Microsoft.EntityFrameworkCore;
using Mt.Domain.ExternalIds;
using Mt.Domain.Migrations;
using Mt.Persistence.ExternalIds;
using Mt.Persistence.Rows;
using Mt.Results;
using Xunit;
using IFetch = Mt.Domain.ExternalIds.IFetch;

namespace Mt.Persistence.Tests;

/// <summary>The external-id ledger's day-one invariants (spec 8).</summary>
[Collection("postgres")]
public class ExternalIdsTests(PostgresFixture fixture)
{
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

    private static IAdd.Request AddRequest(Id migrationId, string value) => new(
        migrationId, ExternalSystem.Source, IdType.PunchCard, IdValue.Create(value).Unwrap());

    private static IFetch.Request FetchRequest(Id migrationId) => new(
        migrationId, ExternalSystem.Source, IdType.PunchCard);

    [Fact]
    public async Task Add_is_idempotent_on_the_same_value()
    {
        var migrationId = await SeedMigrationAsync();
        await using var db = fixture.CreateDbContext();
        var add = new Add(db);

        Assert.True((await add.HandleAsync(AddRequest(migrationId, "PC-1"), default)).IsCompleted(out _, out _));
        await db.SaveChangesAsync();
        Assert.True((await add.HandleAsync(AddRequest(migrationId, "PC-1"), default)).IsCompleted(out _, out _));
        await db.SaveChangesAsync();

        await using var verify = fixture.CreateDbContext();
        Assert.Equal(1, await verify.ExternalIds.CountAsync(e => e.MigrationId == migrationId.Value));
    }

    [Fact]
    public async Task Add_fails_on_a_different_value_for_the_same_key()
    {
        var migrationId = await SeedMigrationAsync();
        await using var db = fixture.CreateDbContext();
        var add = new Add(db);

        Assert.True((await add.HandleAsync(AddRequest(migrationId, "PC-1"), default)).IsCompleted(out _, out _));
        await db.SaveChangesAsync();

        var conflicting = await add.HandleAsync(AddRequest(migrationId, "PC-2"), default);
        Assert.True(conflicting.IsFailed(out _, out var failures));
        Assert.IsType<ConflictingExternalIdFailure>(failures[0]);
    }

    [Fact]
    public async Task Fetch_returns_the_recorded_id_and_fails_when_missing()
    {
        var migrationId = await SeedMigrationAsync();
        await using var db = fixture.CreateDbContext();
        var add = new Add(db);
        Assert.True((await add.HandleAsync(AddRequest(migrationId, "PC-1"), default)).IsCompleted(out _, out _));
        await db.SaveChangesAsync();

        var hit = await new Fetch(db).HandleAsync(FetchRequest(migrationId), default);
        Assert.True(hit.IsCompleted(out var externalId, out _));
        Assert.Equal("PC-1", externalId.Value.Value);

        var missing = await new Fetch(db).HandleAsync(
            new IFetch.Request(migrationId, ExternalSystem.Portal, IdType.CarrierPigeon), default);
        Assert.True(missing.IsFailed(out _, out var failures));
        Assert.IsType<NoExternalIdFailure>(failures[0]);
    }

    [Fact]
    public async Task Cancelled_ids_are_invisible_to_fetch_and_cancel_is_idempotent()
    {
        var migrationId = await SeedMigrationAsync();
        await using var db = fixture.CreateDbContext();
        var add = new Add(db);
        Assert.True((await add.HandleAsync(AddRequest(migrationId, "PC-1"), default)).IsCompleted(out _, out _));
        await db.SaveChangesAsync();

        Assert.True((await new Cancel(db).HandleAsync(migrationId, default)).IsCompleted(out _, out _));
        Assert.True((await new Cancel(db).HandleAsync(migrationId, default)).IsCompleted(out _, out _));

        var fetched = await new Fetch(db).HandleAsync(FetchRequest(migrationId), default);
        Assert.True(fetched.IsFailed(out _, out var failures));
        Assert.IsType<NoExternalIdFailure>(failures[0]);
    }

    [Fact]
    public async Task A_cancelled_migrations_ids_can_be_reclaimed_by_a_new_migration()
    {
        var value = $"PC-{Guid.NewGuid():N}";
        var first = await SeedMigrationAsync();

        await using (var db = fixture.CreateDbContext())
        {
            Assert.True((await new Add(db).HandleAsync(AddRequest(first, value), default)).IsCompleted(out _, out _));
            await db.SaveChangesAsync();
            Assert.True((await new Cancel(db).HandleAsync(first, default)).IsCompleted(out _, out _));
        }

        var second = await SeedMigrationAsync();
        await using var db2 = fixture.CreateDbContext();
        Assert.True((await new Add(db2).HandleAsync(AddRequest(second, value), default)).IsCompleted(out _, out _));
        await db2.SaveChangesAsync(); // the filtered unique index permits the reclaim
    }
}
