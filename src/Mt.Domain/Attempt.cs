using Mt.Results;

namespace Mt.Domain;

/// <summary>
/// A stage's bounded retry counter, part of the idempotency key
/// <c>(MigrationId, DomainEvent, Attempt)</c> (§2.2, §6.1). Pure infrastructure
/// (spec 7): minted at <see cref="First"/> by the outbox, advanced only by the
/// retry scheduler, and consumed by the inbox — handlers never see it.
/// </summary>
public sealed record Attempt
{
    private Attempt(int value) => Value = value;

    /// <summary>The first attempt every stage starts on.</summary>
    public static Attempt First { get; } = new(1);

    public int Value { get; }

    public static Result<Attempt> Create(int value) =>
        value
            .FailWhen(v => v < 1, $"Attempt must be positive but was {value}.")
            .Then(v => new Attempt(v));
}
