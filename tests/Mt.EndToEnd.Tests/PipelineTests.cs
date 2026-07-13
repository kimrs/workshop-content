using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mt.Domain;
using Mt.Domain.Commands;
using Mt.Domain.ExternalIds;
using Mt.Outbox;
using Mt.Persistence;
using Mt.Persistence.Rows;
using Mt.Portal;
using Mt.Processor;
using Mt.Results;
using Mt.Source;
using Mt.Target;
using Testcontainers.PostgreSql;
using Xunit;

namespace Mt.EndToEnd.Tests;

/// <summary>Spins up a throwaway Postgres and applies the EF migration once for the test run.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public WorkshopDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<WorkshopDbContext>().UseNpgsql(ConnectionString).Options);

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

/// <summary>
/// Drives the whole flow through a real host: the Start command writes the outbox, the outbox
/// worker publishes over the InMemory transport (with redelivery on, so the inbox dedup is
/// exercised), the processor consumes through the inbox, and the stage handlers advance the
/// migration (spec 12 D13).
/// </summary>
public sealed class PipelineTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(60);

    private IHost BuildHost(Dictionary<string, string?> overrides)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Transport:Kind"] = "InMemory",
            ["Transport:RedeliverProbability"] = "0.25",
            ["Client:HasAddress"] = "true",
            ["Logging:LogLevel:Default"] = "Warning",
        };
        foreach (var (key, value) in overrides)
        {
            settings[key] = value;
        }

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddPersistence(fixture.ConnectionString);
        builder.Services.AddSimulatedSource(builder.Configuration);
        builder.Services.AddSimulatedTarget(builder.Configuration);
        builder.Services.AddTransport(builder.Configuration);
        builder.Services.AddStageHandlers(builder.Configuration);
        builder.Services.AddPortal();
        builder.Services.AddHostedService<OutboxWorker>();
        builder.Services.AddHostedService<ProcessorWorker>();
        return builder.Build();
    }

    private static IStart.Request StartRequest(string organizationNumber) => new(
        OrganizationNumber.Create(organizationNumber).Unwrap(),
        PunchCardNumber.Create($"PC-{Guid.NewGuid():N}").Unwrap(),
        HoloCrystalId.Create($"HOLO-{Guid.NewGuid():N}").Unwrap(),
        CarrierPigeonTag.Create($"PIGEON-{Guid.NewGuid():N}").Unwrap());

    private async Task<MigrationState> WaitForStateAsync(long migrationId, MigrationState wanted)
    {
        var deadline = DateTimeOffset.UtcNow + PollTimeout;
        var last = MigrationState.Created;
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var db = fixture.CreateDbContext();
            last = await db.Migrations.Where(m => m.Id == migrationId).Select(m => m.State).SingleAsync();
            if (last == wanted)
            {
                return last;
            }

            await Task.Delay(250);
        }

        return last;
    }

    [Fact]
    public async Task Happy_path_with_a_transient_lock_fault_runs_to_completed()
    {
        // FailUntilAttempt = 2: the first Source lock faults, a retry is scheduled and
        // promoted, and the second attempt succeeds — retry-then-succeed, end to end.
        using var host = BuildHost(new() { ["Source:Lock:FailUntilAttempt"] = "2" });
        await host.StartAsync();
        try
        {
            long migrationId;
            using (var scope = host.Services.CreateScope())
            {
                var started = await scope.ServiceProvider.GetRequiredService<IStart>()
                    .HandleAsync(StartRequest($"ORG-{Guid.NewGuid():N}"), default);
                Assert.True(started.IsCompleted(out var id, out var failures),
                    failures is null ? null : failures[0].Message);
                migrationId = id.Value;
            }

            Assert.Equal(MigrationState.SaftUploaded, await WaitForStateAsync(migrationId, MigrationState.SaftUploaded));

            using (var scope = host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<WorkshopDbContext>();
                var org = await db.Migrations.Where(m => m.Id == migrationId)
                    .Select(m => m.OrganizationNumber).SingleAsync();
                var approved = await scope.ServiceProvider.GetRequiredService<IApprove>()
                    .HandleAsync(OrganizationNumber.Create(org).Unwrap(), default);
                Assert.True(approved.IsCompleted(out _, out _));
            }

            Assert.Equal(MigrationState.Completed, await WaitForStateAsync(migrationId, MigrationState.Completed));

            await using var verify = fixture.CreateDbContext();
            // The transient fault really cost an attempt: the lock stage reached attempt 2.
            var lockAttempts = await verify.Inbox
                .Where(i => i.MigrationId == migrationId && i.DomainEvent == "SourceLockRequested")
                .MaxAsync(i => i.Attempt);
            Assert.Equal(2, lockAttempts);
            // Completed migrations keep their external ids claimed (spec 8).
            Assert.Equal(0, await verify.ExternalIds.CountAsync(e => e.MigrationId == migrationId && e.IsCancelled));
        }
        finally
        {
            await host.StopAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task Client_without_address_auto_cancels_and_releases_its_ids()
    {
        using var host = BuildHost(new() { ["Client:HasAddress"] = "false" });
        await host.StartAsync();
        try
        {
            long migrationId;
            using (var scope = host.Services.CreateScope())
            {
                var started = await scope.ServiceProvider.GetRequiredService<IStart>()
                    .HandleAsync(StartRequest($"ORG-{Guid.NewGuid():N}"), default);
                Assert.True(started.IsCompleted(out var id, out var failures),
                    failures is null ? null : failures[0].Message);
                migrationId = id.Value;
            }

            Assert.Equal(MigrationState.Cancelled, await WaitForStateAsync(migrationId, MigrationState.Cancelled));

            await using var verify = fixture.CreateDbContext();
            // Cancellation releases the ledger so the ids can be reclaimed (spec 8).
            Assert.Equal(3, await verify.ExternalIds.CountAsync(e => e.MigrationId == migrationId && e.IsCancelled));
        }
        finally
        {
            await host.StopAsync(TimeSpan.FromSeconds(5));
        }
    }
}

internal static class TestExtensions
{
    public static T Unwrap<T>(this Result<T> result) =>
        result.IsCompleted(out var value, out var failures)
            ? value
            : throw new InvalidOperationException($"Expected completed but was failed: {failures[0].Message}");
}
