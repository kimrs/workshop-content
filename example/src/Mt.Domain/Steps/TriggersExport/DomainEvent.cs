namespace Mt.Domain.Steps.TriggersExport;

/// <summary>Setup fan-out: requests the SAF-T export be triggered on Source (§1.1).</summary>
public sealed record ExportRequested : DomainEvent
{
    public override string ToString() => nameof(ExportRequested);
}
