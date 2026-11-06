using Microsoft.AspNetCore.SignalR;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Infrastructure.RealTime;

public class SignalRInventoryNotifier : IInventoryNotifier
{
    private readonly IHubContext<InventoryHub> _hubContext;
    private readonly ICurrentUserService _currentUserService;

    public SignalRInventoryNotifier(
        IHubContext<InventoryHub> hubContext,
        ICurrentUserService currentUserService)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task NotifyStockUpdateAsync(
        Guid productId,
        int newStock,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            productId = productId.ToString(),
            newStock
        };

        var tenantId = _currentUserService.TenantId;
        if (tenantId != Guid.Empty)
        {
            var groupName = $"tenant_{tenantId.ToString().ToLowerInvariant()}";
            await _hubContext.Clients.Group(groupName).SendAsync("StockDeducted", payload, cancellationToken);
            return;
        }

        await _hubContext.Clients.All.SendAsync("StockDeducted", payload, cancellationToken);
    }
}
