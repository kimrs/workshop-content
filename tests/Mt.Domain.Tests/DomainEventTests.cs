using Mt.Domain.Stages;
using Mt.Domain.Stages.LocksSource;
using Mt.Results;
using Xunit;

namespace Mt.Domain.Tests;

public class DomainEventTests
{
    [Fact]
    public void FromString_resolves_a_known_event()
    {
        Assert.True(DomainEvent.FromString("SourceLockRequested").IsCompleted(out var evt, out _));
        Assert.IsType<SourceLockRequested>(evt);
    }

    [Fact]
    public void FromString_returns_not_found_for_unknown_event()
    {
        Assert.True(DomainEvent.FromString("NopeRequested").IsFailed(out _, out var failures));
        Assert.IsType<NotFoundFailure>(failures[0]);
    }

    [Fact]
    public void ToMessageType_round_trips_through_FromString()
    {
        var original = new SourceLockRequested();
        var roundTripped = DomainEvent.FromString(original.ToMessageType()).Unwrap();
        Assert.Equal(original, roundTripped);
    }
}
