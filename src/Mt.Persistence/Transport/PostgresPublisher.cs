using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mt.Persistence.Rows;
using Mt.Transport;

namespace Mt.Persistence.Transport;

/// <summary>
/// Publishes by inserting into the <c>Messages</c> polling table (§8.3), occasionally twice to
/// exercise the inbox. Shares the outbox worker's context, so it participates in that unit of
/// work. Lives in persistence, not <c>Mt.Transport</c>: the polling table is this adapter's
/// storage choice, and the transport ports stay dependency-free (spec 12 D6).
/// </summary>
public sealed partial class PostgresPublisher(
    WorkshopDbContext db,
    IOptions<TransportSettings> settings,
    ILogger<PostgresPublisher> logger) : IMessagePublisher
{
    public async Task PublishAsync(MessageEnvelope envelope, CancellationToken ct)
    {
        db.Messages.Add(ToRow(envelope));

        if (Redelivery.ShouldRedeliver(settings.Value.RedeliverProbability))
        {
            Log.Redelivering(logger, envelope.DomainEvent, envelope.MigrationId, envelope.Attempt);
            db.Messages.Add(ToRow(envelope));
        }

        await db.SaveChangesAsync(ct);
    }

    private static MessageRow ToRow(MessageEnvelope envelope) => new()
    {
        MigrationId = envelope.MigrationId,
        DomainEvent = envelope.DomainEvent,
        Attempt = envelope.Attempt,
        Payload = envelope.Payload,
        TraceParent = envelope.TraceParent,
        PublishedAt = DateTimeOffset.UtcNow,
    };

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning,
            Message = "🔁 [transport] Redelivering {Event} for migration {MigrationId} attempt {Attempt}.")]
        public static partial void Redelivering(ILogger logger, string @event, long migrationId, int attempt);
    }
}
