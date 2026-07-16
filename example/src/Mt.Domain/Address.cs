using Mt.Results;

namespace Mt.Domain;

/// <summary>
/// A plain value object — the one and only pre-SAF-T validation target (§1.2).
/// No <c>Permissive</c> / degraded-value accumulation; a value is either a valid
/// <see cref="Address"/> or it is not (§10).
/// </summary>
public sealed record Address
{
    private Address(string line, string city)
    {
        Line = line;
        City = city;
    }

    public string Line { get; }

    public string City { get; }

    public static Result<Address> Create(string line, string city) =>
        line
            .FailWhen(string.IsNullOrWhiteSpace, "Address line must not be empty.")
            .Then(_ => city.FailWhen(string.IsNullOrWhiteSpace, "Address city must not be empty."))
            .Then(_ => new Address(line, city));
}
