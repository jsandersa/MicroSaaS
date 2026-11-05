using MediatR;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Core.Events;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.InventoryUpdates;

/// <summary>
/// Manejador de <see cref="ProductPurchasedDomainEvent"/> que incrementa la cantidad comprada
/// en el <c>CurrentStock</c> del producto correspondiente en el inventario del Tenant.
/// </summary>
public class ProductPurchasedEventHandler : INotificationHandler<ProductPurchasedDomainEvent>
{
    private readonly TenantDbContext _context;
    private readonly IInventoryNotifier _notifier;

    public ProductPurchasedEventHandler(TenantDbContext context, IInventoryNotifier notifier)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
    }

    public async Task Handle(ProductPurchasedDomainEvent notification, CancellationToken cancellationToken)
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

        product.AdjustStock(notification.Quantity);

        await _context.SaveChangesAsync(cancellationToken);

        await _notifier.NotifyStockUpdateAsync(product.Id, product.CurrentStock, cancellationToken);
    }
}