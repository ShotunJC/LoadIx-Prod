using System.Security.Claims;
using LoadIx.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LoadIx.Api.Operations;

[Authorize(Policy = LoadIx.Application.Security.LoadIxPermissions.TripView)]
public sealed class OperationsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirstValue(LoadIxClaimTypes.TenantId);
        if (!Guid.TryParse(tenantId, out var parsedTenantId))
        {
            Context.Abort();
            return;
        }

        var userId = Context.User?.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            Context.Abort();
            return;
        }

        // The group is derived only from validated claims, never hub arguments.
        await Groups.AddToGroupAsync(Context.ConnectionId, TenantGroup(parsedTenantId));
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(parsedUserId));
        await base.OnConnectedAsync();
    }

    public static string TenantGroup(Guid tenantId) => $"operations:tenant:{tenantId:N}";

    public static string UserGroup(Guid userId) => $"operations:user:{userId:N}";
}
