using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Mt.Domain;
using Mt.Domain.Migrations;
using Mt.Domain.Stages.UnlocksSource;
using Mt.Results;
using Xunit;
using static Mt.Domain.Tests.TestData;
using Request = Mt.Domain.INotifyCompletion.Request;
using Response = Mt.Domain.Stages.UnlocksSource.IFetchMigration.Response;
using UnlockResponse = Mt.Domain.Stages.UnlocksSource.IUnlockSource.Response;
using SetResponse = Mt.Domain.Stages.UnlocksSource.ISetSourceUnlocked.Response;

namespace Mt.Domain.Tests;

public class UnlocksSourceHandlerTests
{
    private readonly Mock<IFetchMigration> _fetchMigration = new();
    private readonly Mock<IUnlockSource> _unlock = new();
    private readonly Mock<ISetSourceUnlocked> _setUnlocked = new();
    private readonly Mock<ISetCompleted> _setCompleted = new();
    private readonly Mock<ISetCancelled> _setCancelled = new();
    private readonly Mock<ExternalIds.ICancel> _cancelIds = new();
    private readonly Mock<INotifyCompletion> _notify = new();
    private readonly Mock<IScheduleEvent> _schedule = new();
    private readonly Settings _settings = new() { MaxAttempts = 3 };

    public UnlocksSourceHandlerTests()
    {
        _fetchMigration.Setup(f => f.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<Response>)new Response.Proceed());
        _unlock.Setup(u => u.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<UnlockResponse>)new UnlockResponse.Unlocked());
        _setUnlocked.Setup(s => s.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<SetResponse>)new SetResponse.TeardownIncomplete());
        _setCompleted.Setup(s => s.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>())).ReturnsAsync(Ok);
        _setCancelled.Setup(s => s.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>())).ReturnsAsync(Ok);
        _cancelIds.Setup(c => c.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>())).ReturnsAsync(Ok);
        _notify.Setup(n => n.HandleAsync(It.IsAny<Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok);
    }

    private Handler CreateHandler() => new(
        _fetchMigration.Object,
        _unlock.Object,
        _setUnlocked.Object,
        _setCompleted.Object,
        _setCancelled.Object,
        _cancelIds.Object,
        _notify.Object,
        _schedule.Object,
        _settings,
        NullLogger<Handler>.Instance);

    [Fact]
    public async Task Not_last_sibling_sets_flag_without_finalizing()
    {
        // The constructor default: the flag write reports TeardownIncomplete.
        var result = await CreateHandler().HandleAsync(Id(), default);

        Assert.True(result.IsCompleted(out _, out _));
        _setCompleted.Verify(s => s.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()), Times.Never);
        _notify.Verify(n => n.HandleAsync(It.IsAny<Request>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Last_sibling_in_unlocking_completes_and_notifies()
    {
        _setUnlocked.Setup(s => s.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<SetResponse>)new SetResponse.Complete(new Unlocking(Id(), Org(), true, true)));

        var result = await CreateHandler().HandleAsync(Id(), default);

        Assert.True(result.IsCompleted(out _, out _));
        _setCompleted.Verify(s => s.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()), Times.Once);
        _notify.Verify(n => n.HandleAsync(It.IsAny<Request.Migrated>(), It.IsAny<CancellationToken>()), Times.Once);
        // Completed migrations keep their ids claimed permanently (spec 8).
        _cancelIds.Verify(c => c.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Last_sibling_in_cancelling_cancels_and_notifies()
    {
        _setUnlocked.Setup(s => s.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<SetResponse>)new SetResponse.Cancel(new Cancelling(Id(), Org(), true, true)));

        var result = await CreateHandler().HandleAsync(Id(), default);

        Assert.True(result.IsCompleted(out _, out _));
        _setCancelled.Verify(s => s.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()), Times.Once);
        _notify.Verify(n => n.HandleAsync(It.IsAny<Request.Cancelled>(), It.IsAny<CancellationToken>()), Times.Once);
        _cancelIds.Verify(c => c.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Do_not_proceed_is_a_graceful_no_op()
    {
        _fetchMigration.Setup(f => f.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<Response>)new Response.DoNotProceed());

        var result = await CreateHandler().HandleAsync(Id(), default);

        Assert.True(result.IsCompleted(out _, out _));
        _unlock.Verify(
            u => u.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
