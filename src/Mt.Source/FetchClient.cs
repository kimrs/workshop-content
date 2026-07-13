using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mt.Domain;
using Mt.Domain.ExternalIds;
using Mt.Domain.Migrations;
using Mt.Domain.Stages.Transforms;
using Mt.Results;
using IFetch = Mt.Domain.ExternalIds.IFetch;

namespace Mt.Source;

/// <summary>
/// Simulated Source client fetch (§7). Translates the migration id into Source's punch card
/// (spec 8) and returns a client with or without an address per config — this drives the
/// auto-cancel path (§1.2).
/// </summary>
public sealed partial class FetchClient(
    IOptions<ClientSettings> client,
    IFetch fetchExternalId,
    ILogger<FetchClient> logger) : IFetchClient
{
    public async Task<Result<Client>> HandleAsync(Id migrationId, CancellationToken ct)
    {
        var punchCard = await fetchExternalId.HandleAsync(
            new IFetch.Request(migrationId, ExternalSystem.Source, IdType.PunchCard), ct);
        return punchCard.Then(id => FetchByPunchCard(id.Value));
    }

    private Result<Client> FetchByPunchCard(IdValue punchCard)
    {
        var hasAddress = client.Value.HasAddress;
        Log.Fetched(logger, punchCard.Value, hasAddress);

        if (!hasAddress)
        {
            return new Client.WithoutAddress();
        }

        return Address
            .Create("Simulated Street 1", "Simulated City")
            .Map(address => (Client)new Client.WithAddress(address));
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information,
            Message = "📇 [SIM] Fetched client {PunchCard} (hasAddress={HasAddress}).")]
        public static partial void Fetched(ILogger logger, string punchCard, bool hasAddress);
    }
}
