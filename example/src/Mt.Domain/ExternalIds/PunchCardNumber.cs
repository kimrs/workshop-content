using Mt.Results;

namespace Mt.Domain.ExternalIds;

/// <summary>
/// The Source ERP's id for the client being migrated, e.g. <c>PC-1972-0042</c> (spec 8).
/// Supplied by the caller of Start; opaque to us.
/// </summary>
public sealed record PunchCardNumber
{
    private PunchCardNumber(string value) => Value = value;

    public string Value { get; }

    public static Result<PunchCardNumber> Create(string value) =>
        value
            .FailWhen(string.IsNullOrWhiteSpace, "Punch card number must not be empty.")
            .Then(v => v.FailWhen(x => x.Length > IdValue.MaxLength, $"Punch card number must be at most {IdValue.MaxLength} chars."))
            .Then(v => new PunchCardNumber(v));
}
