using Mt.Domain;
using Mt.Domain.Commands;
using Mt.Domain.ExternalIds;
using Mt.Results;
using ICancel = Mt.Domain.Commands.ICancel;

namespace Mt.Api;

/// <summary>The response body for a command: the affected migration's id.</summary>
public sealed record MigrationResponse(long MigrationId);

/// <summary>The Start request body: what each external system calls this migration (spec 8).</summary>
public sealed record StartRequestBody(string PunchCardNumber, string HoloCrystalId, string CarrierPigeonTag);

/// <summary>Maps the three command endpoints (§9). Each parses its inputs, calls a command port, and maps the result.</summary>
public static class MigrationEndpoints
{
    public static IEndpointRouteBuilder MapMigrationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/Migration/{organizationNumber}/Start", async (
            string organizationNumber, StartRequestBody body, IStart start, CancellationToken ct) =>
        {
            var parsed = OrganizationNumber.Create(organizationNumber)
                .Then(org => PunchCardNumber.Create(body.PunchCardNumber)
                    .Then(punchCard => HoloCrystalId.Create(body.HoloCrystalId)
                        .Then(holoCrystal => CarrierPigeonTag.Create(body.CarrierPigeonTag)
                            .Then(pigeonTag => new IStart.Request(org, punchCard, holoCrystal, pigeonTag)))));
            var started = await parsed.ThenAsync(request => start.HandleAsync(request, ct));
            return started.Map(id => new MigrationResponse(id.Value)).ToHttpResult();
        });

        app.MapPost("/Migration/{organizationNumber}/Approve", async (
            string organizationNumber, IApprove approve, CancellationToken ct) =>
        {
            var parsed = OrganizationNumber.Create(organizationNumber);
            var approved = await parsed.ThenAsync(org => approve.HandleAsync(org, ct));
            return approved.Map(id => new MigrationResponse(id.Value)).ToHttpResult();
        });

        app.MapPost("/Migration/{organizationNumber}/Cancel", async (
            string organizationNumber, ICancel cancel, CancellationToken ct) =>
        {
            var parsed = OrganizationNumber.Create(organizationNumber);
            var cancelled = await parsed.ThenAsync(org => cancel.HandleAsync(org, ct));
            return cancelled.Map(id => new MigrationResponse(id.Value)).ToHttpResult();
        });

        return app;
    }
}
