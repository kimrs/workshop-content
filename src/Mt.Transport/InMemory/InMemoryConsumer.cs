namespace Mt.Transport.InMemory;

/// <summary>
/// Invokes the handler for each envelope on the in-process channel (§8.3). Reading pops the
/// message, so an in-flight envelope dies with the process — there is nothing durable to
/// redeliver from. That is inherent to an in-process bus and stated here rather than papered
/// over (spec 12 D3); the Postgres transport is the at-least-once one.
/// </summary>
public sealed class InMemoryConsumer(InMemoryBus bus) : IMessageConsumer
{
    public async Task ConsumeAsync(Func<MessageEnvelope, CancellationToken, Task> handler, CancellationToken ct)
    {
        await foreach (var envelope in bus.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            await handler(envelope, ct).ConfigureAwait(false);
        }
    }
}
