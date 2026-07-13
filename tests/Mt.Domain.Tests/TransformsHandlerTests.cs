using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Mt.Domain;
using Mt.Domain.Migrations;
using Mt.Domain.Stages;
using Mt.Domain.Stages.Transforms;
using Mt.Domain.Stages.UnlocksSource;
using Mt.Domain.Stages.UnlocksTarget;
using Mt.Domain.Stages.UploadsPreSaft;
using Mt.Results;
using Xunit;
using static Mt.Domain.Tests.TestData;
using Handler = Mt.Domain.Stages.Transforms.Handler;
using IFetchMigration = Mt.Domain.Stages.Transforms.IFetchMigration;
using Response = Mt.Domain.Stages.Transforms.IFetchMigration.Response;

namespace Mt.Domain.Tests;

public class TransformsHandlerTests
{
    private readonly Mock<IFetchMigration> _fetchMigration = new();
    private readonly Mock<IFetchClient> _fetchClient = new();
    private readonly Mock<ISetTransformed> _setTransformed = new();
    private readonly Mock<ISetCancelling> _setCancelling = new();
    private readonly Mock<IAdd> _outbox = new();

    private Handler CreateHandler() => new(
        _fetchMigration.Object,
        _fetchClient.Object,
        _setTransformed.Object,
        _setCancelling.Object,
        _outbox.Object,
        NullLogger<Handler>.Instance);

    private void ArrangePrepared() =>
        _fetchMigration.Setup(f => f.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<Response>)new Response.Proceed(new Prepared(Id(), Org())));

    [Fact]
    public async Task Client_without_address_auto_cancels_and_emits_both_unlocks()
    {
        ArrangePrepared();
        _fetchClient.Setup(c => c.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<Client>)new Client.WithoutAddress());
        _setCancelling.Setup(s => s.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>())).ReturnsAsync(Ok);
        _outbox.Setup(o => o.HandleAsync(It.IsAny<Id>(), It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok);

        var result = await CreateHandler().HandleAsync(Id(), default);

        Assert.True(result.IsCompleted(out _, out _));
        _setCancelling.Verify(s => s.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()), Times.Once);
        _setTransformed.Verify(s => s.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()), Times.Never);
        _outbox.Verify(o => o.HandleAsync(It.IsAny<Id>(), It.IsAny<SourceUnlockRequested>(), It.IsAny<CancellationToken>()), Times.Once);
        _outbox.Verify(o => o.HandleAsync(It.IsAny<Id>(), It.IsAny<TargetUnlockRequested>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Client_with_address_advances_and_requests_pre_saft_upload()
    {
        ArrangePrepared();
        _fetchClient.Setup(c => c.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<Client>)new Client.WithAddress(Address()));
        _setTransformed.Setup(s => s.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>())).ReturnsAsync(Ok);
        _outbox.Setup(o => o.HandleAsync(It.IsAny<Id>(), It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok);

        var result = await CreateHandler().HandleAsync(Id(), default);

        Assert.True(result.IsCompleted(out _, out _));
        _setTransformed.Verify(s => s.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()), Times.Once);
        _outbox.Verify(o => o.HandleAsync(It.IsAny<Id>(), It.IsAny<PreSaftUploadRequested>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Do_not_proceed_is_skipped_without_fetching_client()
    {
        _fetchMigration.Setup(f => f.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<Response>)new Response.DoNotProceed());

        var result = await CreateHandler().HandleAsync(Id(), default);

        Assert.True(result.IsCompleted(out _, out _));
        _fetchClient.Verify(c => c.HandleAsync(It.IsAny<Id>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
