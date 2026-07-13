namespace Mt.Source;

/// <summary>Failure configuration for the simulated Source, bound from the <c>Source</c> section (§7.1).</summary>
public sealed class SourceSettings
{
    public OperationFailure Lock { get; set; } = new();

    public OperationFailure TriggerExport { get; set; } = new();

    public OperationFailure Unlock { get; set; } = new();
}

/// <summary>
/// Client configuration, bound from the top-level <c>Client</c> section (§7.1). Drives whether
/// <c>IFetchClient</c> returns a client with or without an address.
/// </summary>
public sealed class ClientSettings
{
    public bool HasAddress { get; set; } = true;
}
