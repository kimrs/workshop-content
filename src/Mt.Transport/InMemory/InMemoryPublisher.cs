using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mt.Transport.InMemory;

/// <summary>Publishes to the in-process channel, occasionally twice to exercise the inbox (§8.3).</summary>
public sealed partial class InMemoryPublisher(
    InMemoryBus bus,
    IOptions<TransportSettings> settings,
    ILogger<InMemoryPublisher> logger) : IMessagePublisher
{
    public async Task PublishAsync(MessageEnvelope envelope, CancellationToken ct)
    {
        await bus.Writer.WriteAsync(envelope, ct);

        if (Redelivery.ShouldRedeliver(settings.Value.RedeliverProbability))
        {
            Log.Redelivering(logger, envelope.DomainEvent, envelope.MigrationId, envelope.Attempt);
            await bus.Writer.WriteAsync(envelope, ct);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning,
            Message = "🔁 [transport] Redelivering {Event} for migration {MigrationId} attempt {Attempt}.")]
        public static partial void Redelivering(ILogger logger, string @event, long migrationId, int attempt);
    }
}
