using Microsoft.Extensions.Logging.Abstractions;
using Mt.Domain.Migrations;
using Mt.Persistence.Rows;
using Mt.Results;
using Xunit;
using LocksSource = Mt.Persistence.Steps.LocksSource;
using Transforms = Mt.Persistence.Steps.Transforms;
using UnlocksSource = Mt.Persistence.Steps.UnlocksSource;

namespace Mt.Persistence.Tests;

/// <summary>
/// Guard mapping of the per-slice <c>FetchMigration</c> adapters (specs 4/5/9), covering the
/// two shapes — forward (<c>LocksSource</c>) and teardown (<c>UnlocksSource</c>) — plus
/// <c>Transforms</c>' Prepared-only admission (spec 9).
/// </summary>
[Collection("postgres")]
public class FetchMigrationTests(PostgresFixture fixture)
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

    private async Task<Result<Mt.Domain.Steps.LocksSource.IFetchMigration.Response>> ForwardAsync(Id id)
    {
        await using var db = fixture.CreateDbContext();
        var port = new LocksSource.FetchMigration(db, NullLogger<LocksSource.FetchMigration>.Instance);
        return await port.HandleAsync(id, default);
    }

    private async Task<Result<Mt.Domain.Steps.UnlocksSource.IFetchMigration.Response>> TeardownAsync(Id id)
    {
        await using var db = fixture.CreateDbContext();
        var port = new UnlocksSource.FetchMigration(db, NullLogger<UnlocksSource.FetchMigration>.Instance);
        return await port.HandleAsync(id, default);
    }

    private async Task<Result<Mt.Domain.Steps.Transforms.IFetchMigration.Response>> TransformsAsync(Id id)
    {
        await using var db = fixture.CreateDbContext();
        var port = new Transforms.FetchMigration(db, NullLogger<Transforms.FetchMigration>.Instance);
        return await port.HandleAsync(id, default);
    }

    [Fact]
    public async Task Forward_slice_maps_working_state_to_proceed()
    {
        var id = await SeedAsync(MigrationState.Created);

        var result = await ForwardAsync(id);

        Assert.IsType<Mt.Domain.Steps.LocksSource.IFetchMigration.Response.Proceed>(result.Unwrap());
    }

    [Fact]
    public async Task Forward_slice_does_not_proceed_when_already_locked()
    {
        var id = await SeedAsync(MigrationState.Created, row => row.SourceLocked = true);

        var result = await ForwardAsync(id);

        Assert.IsType<Mt.Domain.Steps.LocksSource.IFetchMigration.Response.DoNotProceed>(result.Unwrap());
    }

    [Theory]
    [InlineData(MigrationState.Cancelling)]
    [InlineData(MigrationState.Cancelled)]
    public async Task Forward_slice_does_not_proceed_when_cancelling_or_cancelled(MigrationState state)
    {
        var id = await SeedAsync(state);

        var result = await ForwardAsync(id);

        Assert.IsType<Mt.Domain.Steps.LocksSource.IFetchMigration.Response.DoNotProceed>(result.Unwrap());
    }

    [Fact]
    public async Task Forward_slice_fails_on_a_state_the_step_must_never_see()
    {
        var id = await SeedAsync(MigrationState.Transformed);

        var result = await ForwardAsync(id);

        Assert.True(result.IsFailed(out _, out var failures));
        Assert.IsType<MigrationHasIncorrectState>(failures[0]);
    }

    [Fact]
    public async Task Missing_migration_fails_not_found()
    {
        var result = await ForwardAsync(Id.Create(long.MaxValue).Unwrap());

        Assert.True(result.IsFailed(out _, out var failures));
        Assert.IsType<NotFoundFailure>(failures[0]);
    }

    [Fact]
    public async Task Transforms_slice_proceeds_only_for_prepared()
    {
        var id = await SeedAsync(MigrationState.Prepared, row =>
        {
            row.SourceLocked = true;
            row.TargetLocked = true;
            row.ExportTriggered = true;
        });

        var result = await TransformsAsync(id);

        Assert.IsType<Mt.Domain.Steps.Transforms.IFetchMigration.Response.Proceed>(result.Unwrap());
    }

    [Fact]
    public async Task Transforms_slice_fails_on_created_even_with_all_flags_set()
    {
        var id = await SeedAsync(MigrationState.Created, row =>
        {
            row.SourceLocked = true;
            row.TargetLocked = true;
            row.ExportTriggered = true;
        });

        var result = await TransformsAsync(id);

        Assert.True(result.IsFailed(out _, out var failures));
        Assert.IsType<MigrationHasIncorrectState>(failures[0]);
    }

    [Theory]
    [InlineData(MigrationState.Unlocking)]
    [InlineData(MigrationState.Cancelling)]
    public async Task Teardown_slice_proceeds_for_unlocking_and_cancelling(MigrationState state)
    {
        var id = await SeedAsync(state, row => row.TargetUnlocked = true);

        var result = await TeardownAsync(id);

        Assert.IsType<Mt.Domain.Steps.UnlocksSource.IFetchMigration.Response.Proceed>(result.Unwrap().Value);
    }

    [Theory]
    [InlineData(MigrationState.Unlocking)]
    [InlineData(MigrationState.Cancelling)]
    public async Task Teardown_slice_does_not_proceed_when_already_unlocked(MigrationState state)
    {
        var id = await SeedAsync(state, row => row.SourceUnlocked = true);

        var result = await TeardownAsync(id);

        Assert.IsType<Mt.Domain.Steps.UnlocksSource.IFetchMigration.Response.DoNotProceed>(result.Unwrap().Value);
    }

    [Theory]
    [InlineData(MigrationState.Completed)]
    [InlineData(MigrationState.Cancelled)]
    public async Task Teardown_slice_does_not_proceed_when_finalized(MigrationState state)
    {
        var id = await SeedAsync(state);

        var result = await TeardownAsync(id);

        Assert.IsType<Mt.Domain.Steps.UnlocksSource.IFetchMigration.Response.DoNotProceed>(result.Unwrap().Value);
    }

    [Fact]
    public async Task Teardown_slice_fails_on_a_state_the_step_must_never_see()
    {
        var id = await SeedAsync(MigrationState.Created);

        var result = await TeardownAsync(id);

        Assert.True(result.IsFailed(out _, out var failures));
        Assert.IsType<MigrationHasIncorrectState>(failures[0]);
    }
}
