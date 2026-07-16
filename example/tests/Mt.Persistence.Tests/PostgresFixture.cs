using Microsoft.EntityFrameworkCore;
using Mt.Results;
using Testcontainers.PostgreSql;
using Xunit;

namespace Mt.Persistence.Tests;

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

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

internal static class TestExtensions
{
    public static T Unwrap<T>(this Result<T> result) =>
        result.IsCompleted(out var value, out var failures)
            ? value
            : throw new InvalidOperationException($"Expected completed but was failed: {failures[0].Message}");
}
