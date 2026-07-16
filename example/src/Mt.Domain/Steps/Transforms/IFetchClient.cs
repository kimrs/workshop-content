using Mt.Domain.Migrations;
using Mt.Results;

namespace Mt.Domain.Steps.Transforms;

/// <summary>
/// Simulated Source: fetch the client, with or without an address (§6.3). The address
/// presence drives the one and only domain rule (§1.2). The adapter translates the
/// migration id into Source's punch card (spec 8).
/// </summary>
public interface IFetchClient
{
    Task<Result<Client>> HandleAsync(Id migrationId, CancellationToken ct);
}
