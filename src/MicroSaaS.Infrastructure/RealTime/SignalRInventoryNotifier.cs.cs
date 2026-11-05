using Microsoft.AspNetCore.SignalR;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Infrastructure.RealTime;

public class SignalRInventoryNotifier : IInventoryNotifier
{
    private readonly IHubContext<InventoryHub> _hubContext;

    public SignalRInventoryNotifier(IHubContext<InventoryHub> hubContext)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
    }

    public async Task NotifyStockUpdateAsync(Guid productId, int newStock, CancellationToken cancellationToken = default)
    {
        // Empaquetamos en un objeto anónimo para que Angular lo reciba como el modelo StockDeductedEvent
        var payload = new
        {
            productId = productId.ToString(),
            newStock = newStock
        };

        // Emitimos usando el nombre exacto del evento que escucha tu SignalRService.ts
        await _hubContext.Clients.All.SendAsync("StockDeducted", payload, cancellationToken);
    }
}