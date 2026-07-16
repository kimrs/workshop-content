using Mt.Results;

namespace Mt.Domain.ExternalIds;

/// <summary>
/// The Portal's delivery id for completion notices, e.g. <c>PIGEON-OSLO-9</c> (spec 8).
/// Supplied by the caller of Start; opaque to us. Intentional near-duplicate of the other
/// Start input ids (§4.3).
/// </summary>
public sealed record CarrierPigeonTag
{
    private CarrierPigeonTag(string value) => Value = value;

    public string Value { get; }

    public static Result<CarrierPigeonTag> Create(string value) =>
        value
            .FailWhen(string.IsNullOrWhiteSpace, "Carrier pigeon tag must not be empty.")
            .Then(v => v.FailWhen(x => x.Length > IdValue.MaxLength, $"Carrier pigeon tag must be at most {IdValue.MaxLength} chars."))
            .Then(v => new CarrierPigeonTag(v));
}
