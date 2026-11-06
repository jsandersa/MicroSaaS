using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using MicroSaaS.Infrastructure.Authentication;

namespace MicroSaaS.Infrastructure.RealTime;

/// <summary>
/// Hub de SignalR con aislamiento por grupos de Tenant (<c>tenant_id</c>).
/// </summary>
public class InventoryHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var tenantId = Context.User?.FindFirstValue(JwtProvider.TenantIdClaimName)
                       ?? httpContext?.Request.Query["X-Tenant-ID"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{tenantId.Trim().ToLowerInvariant()}");
        }

        await base.OnConnectedAsync();
    }
}
