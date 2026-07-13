namespace Mt.Domain.Stages.UploadsPreSaft;

/// <summary>Requests the pre-SAF-T work be uploaded to Target (§1.1).</summary>
public sealed record PreSaftUploadRequested : DomainEvent
{
    public override string ToString() => nameof(PreSaftUploadRequested);
}
