using Mt.Domain.Migrations;
using Mt.Results;
using Xunit;
using static Mt.Domain.Tests.TestData;

namespace Mt.Domain.Tests;

public class MigrationAggregateTests
{
    [Fact]
    public void Created_is_ready_to_transform_only_when_all_flags_are_set()
    {
        Assert.True(new Created(Id(), Org(), SourceLocked: true, TargetLocked: true, ExportTriggered: true)
            .IsReadyToTransform);
        Assert.False(new Created(Id(), Org(), SourceLocked: true, TargetLocked: false, ExportTriggered: true)
            .IsReadyToTransform);
    }

    [Fact]
    public void Prepared_Transform_advances()
    {
        var prepared = new Prepared(Id(), Org());
        Assert.True(prepared.Transform().IsCompleted(out var transformed, out _));
        Assert.Equal(prepared.Id, transformed.Id);
    }

    [Fact]
    public void Prepared_Cancel_requires_both_systems_unlocked()
    {
        var cancelling = new Prepared(Id(), Org()).Cancel().Unwrap();

        Assert.False(cancelling.SourceUnlocked);
        Assert.False(cancelling.TargetUnlocked);
    }

    [Fact]
    public void Created_Cancel_seeds_unlock_flags_from_what_was_locked()
    {
        // Only Source was locked: Source must be released, Target has nothing to release.
        var created = new Created(Id(), Org(), SourceLocked: true, TargetLocked: false, ExportTriggered: true);

        var cancelling = created.Cancel().Unwrap();

        Assert.False(cancelling.SourceUnlocked); // still needs unlocking
        Assert.True(cancelling.TargetUnlocked);  // nothing to release
        Assert.False(cancelling.IsFullyUnlocked);
    }

    [Fact]
    public void Cancel_from_mid_flight_requires_both_systems_unlocked()
    {
        var transformed = new Transformed(Id(), Org());
        var cancelling = transformed.Cancel().Unwrap();

        Assert.False(cancelling.SourceUnlocked);
        Assert.False(cancelling.TargetUnlocked);
    }

    [Fact]
    public void Unlocking_Complete_requires_full_unlock()
    {
        var partial = new Unlocking(Id(), Org(), SourceUnlocked: true, TargetUnlocked: false);
        Assert.True(partial.Complete().IsFailed(out _, out var failures));
        Assert.IsType<MigrationHasIncorrectState>(failures[0]);

        var full = new Unlocking(Id(), Org(), SourceUnlocked: true, TargetUnlocked: true);
        Assert.True(full.Complete().IsCompleted(out var completed, out _));
        Assert.Equal(full.Id, completed.Id);
    }

    [Fact]
    public void Cancelling_FinalizeCancellation_requires_full_unlock()
    {
        var full = new Cancelling(Id(), Org(), SourceUnlocked: true, TargetUnlocked: true);
        Assert.True(full.FinalizeCancellation().IsCompleted(out var cancelled, out _));
        Assert.Equal(full.Id, cancelled.Id);
    }

    [Fact]
    public void Cancellable_states_expose_Cancel()
    {
        Assert.IsAssignableFrom<ICancellable>(new Created(Id(), Org(), false, false, false));
        Assert.IsAssignableFrom<ICancellable>(new Prepared(Id(), Org()));
        Assert.IsAssignableFrom<ICancellable>(new Transformed(Id(), Org()));
        Assert.IsAssignableFrom<ICancellable>(new PreSaftUploaded(Id(), Org()));
        Assert.IsAssignableFrom<ICancellable>(new SaftUploaded(Id(), Org()));
    }

    [Fact]
    public void Terminal_states_are_not_cancellable()
    {
        Assert.IsNotAssignableFrom<ICancellable>(new Completed(Id(), Org()));
        Assert.IsNotAssignableFrom<ICancellable>(new Cancelled(Id(), Org()));
        Assert.IsNotAssignableFrom<ICancellable>(new Unlocking(Id(), Org(), false, false));
        Assert.IsNotAssignableFrom<ICancellable>(new Cancelling(Id(), Org(), false, false));
    }
}
