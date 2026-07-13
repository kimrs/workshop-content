namespace Mt.Transport;

/// <summary>
/// Decides whether to deliver a message a second time, per the configured probability (§8.3).
/// Public because the publishers live in two assemblies since the transport split (spec 12 D6).
/// </summary>
public static class Redelivery
{
    public static bool ShouldRedeliver(double probability) =>
        probability > 0 && Random.Shared.NextDouble() < probability;
}
