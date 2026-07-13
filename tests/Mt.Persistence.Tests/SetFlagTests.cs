using Microsoft.EntityFrameworkCore;
using Mt.Domain.Migrations;
using Mt.Persistence.Rows;
using Xunit;
using SetLockedResponse = Mt.Domain.Stages.LocksSource.ISetSourceLocked.Response;
using SetUnlockedResponse = Mt.Domain.Stages.UnlocksSource.ISetSourceUnlocked.Response;

namespace Mt.Persistence.Tests;

/// <summary>
/// The fan-in flag writes report what they produced (spec 9): the setup ops perform
/// <c>Created → Prepared</c> atomically with the last flag, the unlock ops report which
/// teardown path finished. <c>SetSourceLocked</c>/<c>SetSourceUnlocked</c> stand in for
/// their intentionally near-duplicated siblings.
/// </summary>
[Collection("postgres")]
public class SetFlagTests(PostgresFixture fixture)
{
    private async Task<Id> SeedAsync(MigrationState state, Action<MigrationRow>? mutate = null)
    {
        await using var db = fixture.CreateDbContext();
        var row = new MigrationRow
        {
            State = state,
            OrganizationNumber = $"ORG-{Guid.NewGuid():N}",
        };
        mutate?.Invoke(row);
        db.Migrations.Add(row);
        await db.SaveChangesAsync();
        return Id.Create(row.Id).Unwrap();
    }

    private async Task<MigrationRow> LoadRowAsync(Id id)
    {
        await using var db = fixture.CreateDbContext();
        return await db.Migrations.SingleAsync(m => m.Id == id.Value);
    }

    [Fact]
    public async Task Last_setup_flag_performs_the_prepared_transition_and_reports_it()
    {
        var id = await SeedAsync(MigrationState.Created, row =>
        {
            row.TargetLocked = true;
            row.ExportTriggered = true;
        });

        await using var db = fixture.CreateDbContext();
        var result = await new Stages.LocksSource.SetSourceLocked(db).HandleAsync(id, default);
        await db.SaveChangesAsync();

        Assert.IsType<SetLockedResponse.SetupComplete>(result.Unwrap());
        var row = await LoadRowAsync(id);
        Assert.Equal(MigrationState.Prepared, row.State);
        Assert.True(row.SourceLocked);
    }

    [Fact]
    public async Task Earlier_setup_flag_reports_incomplete_and_keeps_created()
    {
        var id = await SeedAsync(MigrationState.Created, row => row.ExportTriggered = true);

        await using var db = fixture.CreateDbContext();
        var result = await new Stages.LocksSource.SetSourceLocked(db).HandleAsync(id, default);
        await db.SaveChangesAsync();

        Assert.IsType<SetLockedResponse.SetupIncomplete>(result.Unwrap());
        var row = await LoadRowAsync(id);
        Assert.Equal(MigrationState.Created, row.State);
        Assert.True(row.SourceLocked);
    }

    [Fact]
    public async Task Setup_flag_on_a_cancelling_migration_reports_incomplete_and_keeps_the_state()
    {
        var id = await SeedAsync(MigrationState.Cancelling, row =>
        {
            row.TargetLocked = true;
            row.ExportTriggered = true;
        });

        await using var db = fixture.CreateDbContext();
        var result = await new Stages.LocksSource.SetSourceLocked(db).HandleAsync(id, default);
        await db.SaveChangesAsync();

        Assert.IsType<SetLockedResponse.SetupIncomplete>(result.Unwrap());
        Assert.Equal(MigrationState.Cancelling, (await LoadRowAsync(id)).State);
    }

    [Fact]
    public async Task Last_unlock_on_the_approve_path_reports_complete()
    {
        var id = await SeedAsync(MigrationState.Unlocking, row => row.TargetUnlocked = true);

        await using var db = fixture.CreateDbContext();
        var result = await new Stages.UnlocksSource.SetSourceUnlocked(db).HandleAsync(id, default);
        await db.SaveChangesAsync();

        var complete = Assert.IsType<SetUnlockedResponse.Complete>(result.Unwrap());
        Assert.True(complete.Migration.IsFullyUnlocked);
        Assert.True((await LoadRowAsync(id)).SourceUnlocked);
    }

    [Fact]
    public async Task Last_unlock_on_the_cancel_path_reports_cancel()
    {
        var id = await SeedAsync(MigrationState.Cancelling, row => row.TargetUnlocked = true);

        await using var db = fixture.CreateDbContext();
        var result = await new Stages.UnlocksSource.SetSourceUnlocked(db).HandleAsync(id, default);
        await db.SaveChangesAsync();

        var cancel = Assert.IsType<SetUnlockedResponse.Cancel>(result.Unwrap());
        Assert.True(cancel.Migration.IsFullyUnlocked);
    }

    [Fact]
    public async Task Earlier_unlock_reports_teardown_incomplete()
    {
        var id = await SeedAsync(MigrationState.Unlocking);

        await using var db = fixture.CreateDbContext();
        var result = await new Stages.UnlocksSource.SetSourceUnlocked(db).HandleAsync(id, default);
        await db.SaveChangesAsync();

        Assert.IsType<SetUnlockedResponse.TeardownIncomplete>(result.Unwrap());
        Assert.True((await LoadRowAsync(id)).SourceUnlocked);
    }
}
