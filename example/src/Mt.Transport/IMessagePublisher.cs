namespace Mt.Transport;

/// <summary>Publishes an envelope to the transport (used by <c>Mt.Outbox</c>, §8.3).</summary>
public interface IMessagePublisher
{
    Task PublishAsync(MessageEnvelope envelope, CancellationToken ct);
}
