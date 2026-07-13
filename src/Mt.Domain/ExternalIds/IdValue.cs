using Mt.Results;

namespace Mt.Domain.ExternalIds;

/// <summary>An external system's id token: opaque to us, non-empty, at most 100 chars (spec 8).</summary>
public sealed record IdValue
{
    public const int MaxLength = 100;

    private IdValue(string value) => Value = value;

    public string Value { get; }

    public static Result<IdValue> Create(string value) =>
        value
            .FailWhen(string.IsNullOrWhiteSpace, "External id value must not be empty.")
            .Then(v => v.FailWhen(x => x.Length > MaxLength, $"External id value must be at most {MaxLength} chars."))
            .Then(v => new IdValue(v));
}
