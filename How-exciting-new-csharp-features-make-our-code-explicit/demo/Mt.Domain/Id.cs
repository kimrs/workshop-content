namespace Mt.Domain;

/// <summary>Identity of a migration: a positive <see cref="long"/>.</summary>
public sealed record Id
{
    private Id(long value) => Value = value;

    public long Value { get; }

    public static Id Create(long value) =>
        value > 0
            ? new Id(value)
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Migration id must be positive.");
}
