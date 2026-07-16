using System.Threading.Channels;

namespace Mt.Transport.InMemory;

/// <summary>
/// A single in-process channel shared by the in-memory publisher and consumer (§8.3). Registered as
/// a singleton so publish and consume see the same bus when the whole app runs in one host.
/// </summary>
public sealed class InMemoryBus
{
    private readonly Channel<MessageEnvelope> _channel = Channel.CreateUnbounded<MessageEnvelope>();

    public ChannelWriter<MessageEnvelope> Writer => _channel.Writer;

    public ChannelReader<MessageEnvelope> Reader => _channel.Reader;
}
