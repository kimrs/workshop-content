using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mt.Persistence.Outboxes;
using Mt.Persistence.Rows;
using Mt.Persistence;
using Mt.Transport;

namespace Mt.Outbox;

/// <summary>
/// Promotes due retries into the outbox, then publishes pending outbox rows via the transport (§8.2).
/// At-least-once: a crash between publish and mark-processed re-publishes — which is exactly why the
/// inbox exists. Redelivery probability (§8.3) also injects duplicates on purpose.
/// </summary>
public sealed partial class OutboxWorker(IServiceScopeFactory scopeFactory, ILogger<OutboxWorker> logger)
    : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("📮 Outbox worker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox poll failed.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<Scheduler>();
        var store = scope.ServiceProvider.GetRequiredService<OutboxStore>();
        var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

        await scheduler.PromoteDueAsync(BatchSize, ct);

        var pending = await store.FetchPendingAsync(BatchSize, ct);
        foreach (var row in pending)
        {
            try
            {
                await publisher.PublishAsync(row.ToEnvelope(), ct);
                await store.MarkProcessedAsync(row.Id, ct);
                Log.Published(logger, row.DomainEvent, row.MigrationId, row.Attempt);
            }
            catch (Exception exception)
            {
                Log.PublishFailed(logger, exception, row.Id);
                await store.MarkFailedAsync(row.Id, exception.Message, ct);
            }
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug,
            Message = "Published {Event} for migration {MigrationId} attempt {Attempt}.")]
        public static partial void Published(ILogger logger, string @event, long migrationId, int attempt);

        [LoggerMessage(Level = LogLevel.Error,
            Message = "Failed to publish outbox row {OutboxId}.")]
        public static partial void PublishFailed(ILogger logger, Exception exception, long outboxId);
    }
}

/// <summary>Manual mapping from a stored outbox row to the wire envelope (§4.7).</summary>
internal static class OutboxMapping
{
    extension(OutboxRow row)
    {
        public MessageEnvelope ToEnvelope() => new()
        {
            MigrationId = row.MigrationId,
            DomainEvent = row.DomainEvent,
            Attempt = row.Attempt,
            Payload = row.Payload,
            TraceParent = row.TraceParent,
        };
    }
}
