using Mt.Results;

namespace Mt.Domain.ExternalIds;

/// <summary>
/// The external systems that mint their own id for a migration (spec 8). Closed set:
/// fixed singletons plus a total <see cref="Create"/> for rehydration — an unknown
/// string from the DB is corrupt data, surfaced as a failure, never a new case.
/// (Named <c>ExternalSystem</c>, not the pattern doc's <c>System</c>, to avoid
/// colliding with the <c>System</c> namespace.)
/// </summary>
public sealed record ExternalSystem
{
    private ExternalSystem(string value) => Value = value;

    public static ExternalSystem Source { get; } = new("Source");

    public static ExternalSystem Target { get; } = new("Target");

    public static ExternalSystem Portal { get; } = new("Portal");

    public string Value { get; }

    public static Result<ExternalSystem> Create(string value) => value switch
    {
        "Source" => Source,
        "Target" => Target,
        "Portal" => Portal,
        _ => (Result<ExternalSystem>)new ValidationFailure($"Unknown external system '{value}'."),
    };
}
