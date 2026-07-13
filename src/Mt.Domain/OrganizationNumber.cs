using Mt.Results;

namespace Mt.Domain;

/// <summary>
/// Identifies the client being migrated. A plain non-empty string — no Norwegian
/// Mod-11 checksum and no <c>Unchecked</c>/<c>Norwegian</c> hierarchy (§6.1, §10).
/// </summary>
public sealed record OrganizationNumber
{
    private OrganizationNumber(string value) => Value = value;

    public string Value { get; }

    public static Result<OrganizationNumber> Create(string value) =>
        value
            .FailWhen(string.IsNullOrWhiteSpace, "Organization number must not be empty.")
            .Then(v => new OrganizationNumber(v));
}
