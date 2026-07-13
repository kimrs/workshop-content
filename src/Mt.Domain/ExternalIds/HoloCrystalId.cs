using Mt.Results;

namespace Mt.Domain.ExternalIds;

/// <summary>
/// The Target cloud ERP's id for the tenant being migrated into, e.g. <c>HOLO-7F3A-CAFE</c>
/// (spec 8). Supplied by the caller of Start; opaque to us. Intentional near-duplicate of
/// the other Start input ids (§4.3).
/// </summary>
public sealed record HoloCrystalId
{
    private HoloCrystalId(string value) => Value = value;

    public string Value { get; }

    public static Result<HoloCrystalId> Create(string value) =>
        value
            .FailWhen(string.IsNullOrWhiteSpace, "Holo-crystal id must not be empty.")
            .Then(v => v.FailWhen(x => x.Length > IdValue.MaxLength, $"Holo-crystal id must be at most {IdValue.MaxLength} chars."))
            .Then(v => new HoloCrystalId(v));
}
