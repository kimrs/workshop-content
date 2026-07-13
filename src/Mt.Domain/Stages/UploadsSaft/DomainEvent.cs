namespace Mt.Domain.Stages.UploadsSaft;

/// <summary>Requests the SAF-T file be downloaded from Source and uploaded to Target (§1.1).</summary>
public sealed record SaftUploadRequested : DomainEvent
{
    public override string ToString() => nameof(SaftUploadRequested);
}
