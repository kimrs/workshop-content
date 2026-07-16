namespace Mt.Domain.Steps.Transforms;

/// <summary>Fan-in target of setup: transform Source data into Target shape (§1.1).</summary>
public sealed record TransformRequested : DomainEvent
{
    public override string ToString() => nameof(TransformRequested);
}
