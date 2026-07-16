namespace Mt.Transport;

/// <summary>
/// Transport configuration (§8.3). <see cref="Kind"/> selects <c>InMemory</c> (default) or
/// <c>Postgres</c>. <see cref="RedeliverProbability"/> intentionally delivers some messages twice
/// so the inbox dedup is observably exercised during the workshop.
/// </summary>
public sealed class TransportSettings
{
    public string Kind { get; set; } = "InMemory";

    public double RedeliverProbability { get; set; }

    public int PollBatchSize { get; set; } = 20;

    public int PollIntervalMs { get; set; } = 500;
}
