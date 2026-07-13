namespace Mt.Domain.Stages;

/// <summary>
/// The canonical wire name of a <see cref="DomainEvent"/>, paired with
/// <see cref="DomainEvent.FromString"/>. Written with the C# 14 <c>extension(...)</c>
/// block syntax mandated by §4.8.
/// </summary>
public static class Extensions
{
    extension(DomainEvent domainEvent)
    {
        public string ToMessageType() => domainEvent.ToString();
    }
}
