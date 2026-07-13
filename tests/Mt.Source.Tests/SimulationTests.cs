using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mt.Domain;
using Mt.Domain.ExternalIds;
using Mt.Domain.Migrations;
using Mt.Results;
using Xunit;
using IFetch = Mt.Domain.ExternalIds.IFetch;
using LockResponse = Mt.Domain.Stages.LocksSource.ILockSource.Response;

namespace Mt.Source.Tests;

public class SimulationTests
{
    private const string PunchCard = "PC-42";

    /// <summary>Resolves every migration id to <c>PC-&lt;id&gt;</c>, like the real ledger would.</summary>
    private sealed class FakeFetch : IFetch
    {
        public Task<Result<ExternalId>> HandleAsync(IFetch.Request request, CancellationToken ct) =>
            Task.FromResult(
                IdValue.Create($"PC-{request.MigrationId.Value}")
                    .Then(value => ExternalId.Create(request.MigrationId, request.System, request.Type, value)));
    }

    private static Id Id(long value = 42) => Domain.Migrations.Id.Create(value).ToValue();

    private static LockSource Lock(SourceSettings settings) =>
        new(Options.Create(settings), new FakeFetch(), new Simulator(), NullLogger<LockSource>.Instance);

    [Fact]
    public async Task FailUntilAttempt_faults_before_and_locks_from_the_threshold()
    {
        var settings = new SourceSettings { Lock = new OperationFailure { FailUntilAttempt = 2 } };
        var lockSource = Lock(settings);

        var faulted = Assert.IsType<LockResponse.Faulted>((await lockSource.HandleAsync(Id(), default)).ToValue());
        Assert.Contains(PunchCard, faulted.Reason);
        Assert.IsType<LockResponse.Locked>((await lockSource.HandleAsync(Id(), default)).ToValue());
    }

    [Fact]
    public async Task Simulator_counts_operations_independently_per_punch_card()
    {
        var settings = new SourceSettings { Lock = new OperationFailure { FailUntilAttempt = 2 } };
        var lockSource = Lock(settings);

        Assert.IsType<LockResponse.Faulted>((await lockSource.HandleAsync(Id(1), default)).ToValue());
        Assert.IsType<LockResponse.Faulted>((await lockSource.HandleAsync(Id(2), default)).ToValue());
        Assert.IsType<LockResponse.Locked>((await lockSource.HandleAsync(Id(1), default)).ToValue());
    }

    [Fact]
    public async Task AlwaysFail_faults_on_every_call()
    {
        var settings = new SourceSettings { Lock = new OperationFailure { AlwaysFail = true } };
        var lockSource = Lock(settings);

        Assert.IsType<LockResponse.Faulted>((await lockSource.HandleAsync(Id(), default)).ToValue());
        Assert.IsType<LockResponse.Faulted>((await lockSource.HandleAsync(Id(), default)).ToValue());
    }

    // The simulator's OperationThrew reaches the domain only as the fault's reason text (spec 11 D7).
    [Fact]
    public async Task Throw_mode_faults_with_the_threw_reason()
    {
        var settings = new SourceSettings { Lock = new OperationFailure { AlwaysFail = true, Throw = true } };
        var lockSource = Lock(settings);

        var faulted = Assert.IsType<LockResponse.Faulted>((await lockSource.HandleAsync(Id(), default)).ToValue());
        Assert.Contains("threw", faulted.Reason);
    }

    [Fact]
    public async Task No_config_always_locks()
    {
        var lockSource = Lock(new SourceSettings());
        Assert.IsType<LockResponse.Locked>((await lockSource.HandleAsync(Id(), default)).ToValue());
    }

    [Fact]
    public async Task FetchClient_returns_client_with_or_without_address_per_config()
    {
        var withAddress = new FetchClient(
            Options.Create(new ClientSettings { HasAddress = true }), new FakeFetch(), NullLogger<FetchClient>.Instance);
        Assert.True((await withAddress.HandleAsync(Id(), default)).IsCompleted(out var client, out _));
        Assert.IsType<Client.WithAddress>(client);

        var withoutAddress = new FetchClient(
            Options.Create(new ClientSettings { HasAddress = false }), new FakeFetch(), NullLogger<FetchClient>.Instance);
        Assert.True((await withoutAddress.HandleAsync(Id(), default)).IsCompleted(out var noAddress, out _));
        Assert.IsType<Client.WithoutAddress>(noAddress);
    }

    [Fact]
    public async Task DownloadSaft_names_the_file_after_the_punch_card()
    {
        var download = new DownloadSaft(new FakeFetch(), NullLogger<DownloadSaft>.Instance);

        Assert.True((await download.HandleAsync(Id(), default)).IsCompleted(out var file, out _));
        Assert.Equal($"{PunchCard}.xml", file.FileName);
    }
}

internal static class TestExtensions
{
    public static T ToValue<T>(this Result<T> result) =>
        result.IsCompleted(out var value, out _) ? value : throw new InvalidOperationException("expected completed");
}
