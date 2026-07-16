using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mt.Persistence.Rows;
using Mt.Transport;

namespace Mt.Persistence.Transport;

/// <summary>
/// Polls the <c>Messages</c> table and invokes the handler per row, marking <c>ConsumedAt</c>
/// only <em>after</em> the handler returns (spec 12 D2) — at-least-once: a crash mid-handling
/// leaves the row unconsumed, it is redelivered on the next poll, and the inbox dedups.
/// One row is marked at a time so a crash can only redeliver in-flight work. Uses a fresh
/// scope per poll so the context is short-lived.
/// </summary>
public sealed class PostgresConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<TransportSettings> settings) : IMessageConsumer
{
    public async Task ConsumeAsync(Func<MessageEnvelope, CancellationToken, Task> handler, CancellationToken ct)
    {
        var options = settings.Value;
        while (!ct.IsCancellationRequested)
        {
            var handled = await PollOnceAsync(handler, options.PollBatchSize, ct);
            if (handled == 0)
            {
                await Task.Delay(options.PollIntervalMs, ct);
            }
        }
    }

    private async Task<int> PollOnceAsync(
        Func<MessageEnvelope, CancellationToken, Task> handler, int batchSize, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkshopDbContext>();

        var rows = await db.Messages
            .Where(m => m.ConsumedAt == null)
            .OrderBy(m => m.Id)
            .Take(batchSize)
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            await handler(ToEnvelope(row), ct);
            row.ConsumedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return rows.Count;
    }

    private static MessageEnvelope ToEnvelope(MessageRow row) => new()
    {
        MigrationId = row.MigrationId,
        DomainEvent = row.DomainEvent,
        Attempt = row.Attempt,
        Payload = row.Payload,
        TraceParent = row.TraceParent,
    };
}
