using Mt.Results;

namespace Mt.Domain.Migrations;

/// <summary>Identity of a migration: a positive <see cref="long"/> (§6.1).</summary>
public sealed record Id
{
    private Id(long value) => Value = value;

    public long Value { get; }

    public static Result<Id> Create(long value) =>
        value
            .FailWhen(v => v <= 0, $"Migration id must be positive but was {value}.")
            .Then(v => new Id(v));
}
