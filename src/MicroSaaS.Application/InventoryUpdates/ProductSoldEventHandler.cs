using MediatR;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Core.Events;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.InventoryUpdates;

public class ProductSoldEventHandler : INotificationHandler<ProductSoldDomainEvent>
{
    private readonly TenantDbContext _context;
    private readonly IInventoryNotifier _notifier;

    public ProductSoldEventHandler(TenantDbContext context, IInventoryNotifier notifier)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
    }

    public async Task Handle(ProductSoldDomainEvent notification, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(notification.Sku) || notification.Quantity <= 0)
        {
            return;
        }

        var normalizedSku = notification.Sku.Trim().ToUpperInvariant();
        var rawSkuOrName = notification.Sku.Trim();

        var product = await _context.Products
            .FirstOrDefaultAsync(
                p => (string.IsNullOrEmpty(notification.TenantId) || p.TenantId == notification.TenantId)
                     && (p.Sku == normalizedSku || p.Name == rawSkuOrName),
                cancellationToken);

        if (product is null)
        {
            return;
        }

        product.AdjustStock(-notification.Quantity);

        await _context.SaveChangesAsync(cancellationToken);

        // Emitimos el evento de actualización en tiempo real una vez persistido el descuento
        await _notifier.NotifyStockUpdateAsync(product.Id, product.CurrentStock, cancellationToken);
    }
}