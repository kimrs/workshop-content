namespace Mt.Transport;

/// <summary>
/// Consumes envelopes from the transport (used by <c>Mt.Processor</c>, §8.3, §8.5). The
/// consumer brackets the handler so acknowledgement can follow processing (spec 12 D2):
/// a durable transport marks a message consumed only after the handler returns, making
/// consumption at-least-once — a crash mid-handling redelivers, and the inbox dedups.
/// </summary>
public interface IMessageConsumer
{
    Task ConsumeAsync(Func<MessageEnvelope, CancellationToken, Task> handler, CancellationToken ct);
}
