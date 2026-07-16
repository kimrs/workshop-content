namespace Mt.Domain;

/// <summary>
/// The client fetched from Source. Optionality of the address is modeled with
/// subtypes, never a nullable property (§4.5, §6.1). <c>Transform</c> branches on
/// which subtype it gets. Carries no identity: Source knows the client by punch
/// card, and that stays inside the Source adapter (spec 8).
/// </summary>
public abstract record Client
{
    private Client()
    {
    }

    public sealed record WithAddress(Address Address) : Client;

    public sealed record WithoutAddress : Client;
}
