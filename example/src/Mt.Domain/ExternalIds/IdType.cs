using Mt.Results;

namespace Mt.Domain.ExternalIds;

/// <summary>
/// What kind of id an external system knows a migration by (spec 8). Closed set, one per
/// system today — kept separate from <see cref="ExternalSystem"/> because the pair is the
/// contract surface. No year-scoped subtype: this app has no per-instance dimension.
/// </summary>
public sealed record IdType
{
    private IdType(string value) => Value = value;

    /// <summary>The legacy Source ERP files everything on punch cards.</summary>
    public static IdType PunchCard { get; } = new("PunchCard");

    /// <summary>The Target cloud ERP stores tenants in holo-crystals.</summary>
    public static IdType HoloCrystal { get; } = new("HoloCrystal");

    /// <summary>The Portal delivers completion notices by carrier pigeon.</summary>
    public static IdType CarrierPigeon { get; } = new("CarrierPigeon");

    public string Value { get; }

    public static Result<IdType> Create(string value) => value switch
    {
        "PunchCard" => PunchCard,
        "HoloCrystal" => HoloCrystal,
        "CarrierPigeon" => CarrierPigeon,
        _ => (Result<IdType>)new ValidationFailure($"Unknown external id type '{value}'."),
    };
}
