using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mt.Domain;
using Mt.Domain.ExternalIds;
using Mt.Domain.Migrations;
using Mt.Results;
using Xunit;
using IFetch = Mt.Domain.ExternalIds.IFetch;
using UnlockResponse = Mt.Domain.Steps.UnlocksTarget.IUnlockTarget.Response;

namespace Mt.Marble.Tests;

public class SimulationTests
{
    /// <summary>Resolves every migration id to <c>HOLO-&lt;id&gt;</c>, like the real ledger would.</summary>
    private sealed class FakeFetch : IFetch
    {
        public Task<Result<ExternalId>> HandleAsync(IFetch.Request request, CancellationToken ct) =>
            Task.FromResult(
                IdValue.Create($"HOLO-{request.MigrationId.Value}")
                    .Then(value => ExternalId.Create(request.MigrationId, request.System, request.Type, value)));
    }

    private static Id Id(long value = 0x7F3A) => Domain.Migrations.Id.Create(value).ToValue();

    private static UnlockTarget Unlock(TargetSettings settings) =>
        new(Options.Create(settings), new FakeFetch(), new Simulator(), NullLogger<UnlockTarget>.Instance);

    [Fact]
    public async Task FailUntilAttempt_faults_before_and_unlocks_from_the_threshold()
    {
        var unlock = Unlock(new TargetSettings { Unlock = new OperationFailure { FailUntilAttempt = 3 } });

        Assert.IsType<UnlockResponse.Faulted>((await unlock.HandleAsync(Id(), default)).ToValue());
        Assert.IsType<UnlockResponse.Faulted>((await unlock.HandleAsync(Id(), default)).ToValue());
        Assert.IsType<UnlockResponse.Unlocked>((await unlock.HandleAsync(Id(), default)).ToValue());
    }

    [Fact]
    public async Task AlwaysFail_faults_on_every_call()
    {
        var unlock = Unlock(new TargetSettings { Unlock = new OperationFailure { AlwaysFail = true } });

        Assert.IsType<UnlockResponse.Faulted>((await unlock.HandleAsync(Id(), default)).ToValue());
        Assert.IsType<UnlockResponse.Faulted>((await unlock.HandleAsync(Id(), default)).ToValue());
    }

    [Fact]
    public async Task Uploads_always_succeed()
    {
        var preSaft = new UploadPreSaft(new FakeFetch(), NullLogger<UploadPreSaft>.Instance);
        Assert.True((await preSaft.HandleAsync(Id(), default)).IsCompleted(out _, out _));

        var saft = new UploadSaft(new FakeFetch(), NullLogger<UploadSaft>.Instance);
        var file = SaftFile.Create("f.xml", [1, 2, 3]).ToValue();
        Assert.True((await saft.HandleAsync(Id(), file, default)).IsCompleted(out _, out _));
    }
}

internal static class TestExtensions
{
    public static T ToValue<T>(this Result<T> result) =>
        result.IsCompleted(out var value, out _) ? value : throw new InvalidOperationException("expected completed");
}
