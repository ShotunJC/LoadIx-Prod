using LoadIx.Application.System;
using LoadIx.Contracts.System;

namespace LoadIx.Api.Endpoints;

public static class SystemEndpoints
{
    public static RouteGroupBuilder MapSystemEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/system/info",
                async (
                    ISystemInfoQueryHandler handler,
                    CancellationToken cancellationToken) =>
                {
                    var snapshot = await handler.HandleAsync(cancellationToken);

                    return TypedResults.Ok(new SystemInfoResponse(
                        snapshot.ServiceName,
                        snapshot.Version,
                        snapshot.ApiVersion,
                        snapshot.ServerTimeUtc));
                })
            .WithName("GetSystemInfo")
            .WithSummary("Returns safe public metadata about the LoadIx API.")
            .Produces<SystemInfoResponse>();

        return group;
    }
}
