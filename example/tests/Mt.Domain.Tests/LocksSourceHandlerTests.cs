using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Mt.Domain;
using Mt.Domain.Migrations;
using Mt.Domain.Steps;
using Mt.Domain.Steps.LocksSource;
using Mt.Domain.Steps.Transforms;
using Mt.Results;
using Xunit;
using static Mt.Domain.Tests.TestData;
using Handler = Mt.Domain.Steps.LocksSource.Handler;
using IFetchMigration = Mt.Domain.Steps.LocksSource.IFetchMigration;
using LockResponse = Mt.Domain.Steps.LocksSource.ILockSource.Response;
using Response = Mt.Domain.Steps.LocksSource.IFetchMigration.Response;
using SetResponse = Mt.Domain.Steps.LocksSource.ISetSourceLocked.Response;

namespace Mt.Domain.Tests;

public class LocksSourceHandlerTests
{
    private readonly Mock<IFetchMigration> _fetchMigration = new();
    private readonly Mock<ILockSource> _lockSource = new();
    private readonly Mock<ISetSourceLocked> _setLocked = new();
    private readonly Mock<IAdd> _outbox = new();
    private readonly Mock<IScheduleEvent> _schedule = new();
    private readonly Settings _settings = new() { MaxAttempts = 3 };

    private Handler CreateHandler() => new(
        _fetchMigration.Object,
        _lockSource.Object,
        _setLocked.Object,
        _outbox.Object,
        _schedule.Object,
        _settings,
        NullLogger<Handler>.Instance);

    private void ArrangeProceed() =>
        _fetchMigration.Setup(f => f.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<Response>)new Response.Proceed());

    [Fact]
    public async Task Success_without_all_siblings_sets_flag_and_does_not_emit_transform()
    {
        ArrangeProceed();
        _lockSource.Setup(l => l.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<LockResponse>)new LockResponse.Locked());
        _setLocked.Setup(s => s.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<SetResponse>)new SetResponse.SetupIncomplete());

        var result = await CreateHandler().HandleAsync(Id(), default);

        Assert.True(result.IsCompleted(out _, out _));
        _setLocked.Verify(s => s.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()), Times.Once);
        _outbox.Verify(
            o => o.HandleAsync(It.IsAny<Id>(), It.IsAny<TransformRequested>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Success_with_last_sibling_fans_in_and_emits_transform()
    {
        ArrangeProceed();
        _lockSource.Setup(l => l.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<LockResponse>)new LockResponse.Locked());
        _setLocked.Setup(s => s.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<SetResponse>)new SetResponse.SetupComplete());
        _outbox.Setup(o => o.HandleAsync(It.IsAny<Id>(), It.IsAny<TransformRequested>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok);

        var result = await CreateHandler().HandleAsync(Id(), default);

        Assert.True(result.IsCompleted(out _, out _));
        _outbox.Verify(
            o => o.HandleAsync(It.IsAny<Id>(), It.Is<TransformRequested>(_ => true), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Failure_with_budget_left_schedules_a_retry()
    {
        ArrangeProceed();
        _lockSource.Setup(l => l.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<LockResponse>)new LockResponse.Faulted("simulated fault"));
        _schedule.Setup(s => s.HandleAsync(It.IsAny<Id>(), It.IsAny<SourceLockRequested>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<IScheduleEvent.Response>)new IScheduleEvent.Response.Scheduled(Attempt(2)));

        var result = await CreateHandler().HandleAsync(Id(), default);

        Assert.True(result.IsCompleted(out _, out _));
        _schedule.Verify(
            s => s.HandleAsync(It.IsAny<Id>(), It.Is<SourceLockRequested>(_ => true), _settings.MaxAttempts, It.IsAny<CancellationToken>()),
            Times.Once);
        _setLocked.Verify(s => s.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Failure_with_budget_spent_returns_out_of_retries()
    {
        ArrangeProceed();
        _lockSource.Setup(l => l.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<LockResponse>)new LockResponse.Faulted("simulated fault"));
        _schedule.Setup(s => s.HandleAsync(It.IsAny<Id>(), It.IsAny<SourceLockRequested>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<IScheduleEvent.Response>)new IScheduleEvent.Response.Exhausted());

        var result = await CreateHandler().HandleAsync(Id(), default);

        Assert.True(result.IsFailed(out _, out var failures));
        Assert.IsType<OutOfRetriesFailure>(failures[0]);
        _setLocked.Verify(s => s.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Do_not_proceed_is_a_graceful_no_op()
    {
        _fetchMigration.Setup(f => f.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<Response>)new Response.DoNotProceed());

        var result = await CreateHandler().HandleAsync(Id(), default);

        Assert.True(result.IsCompleted(out _, out _));
        _lockSource.Verify(
            l => l.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
