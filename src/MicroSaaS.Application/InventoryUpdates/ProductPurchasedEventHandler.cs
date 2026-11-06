using MediatR;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Enums;
using MicroSaaS.Core.Events;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.InventoryUpdates;

/// <summary>
/// Manejador de <see cref="ProductPurchasedDomainEvent"/> que registra la entrada de inventario en el producto
/// recalculando dinámicamente su Precio Medio Ponderado (PMP) y su Precio de Venta con el margen parametrizado del Tenant,
/// inserta la transacción inmutable en <see cref="KardexTransaction"/> y notifica en tiempo real vía SignalR.
/// </summary>
public class ProductPurchasedEventHandler : INotificationHandler<ProductPurchasedDomainEvent>
{
    private readonly TenantDbContext _context;
    private readonly IInventoryNotifier _notifier;
    private readonly ICurrentUserService _currentUserService;

    public ProductPurchasedEventHandler(
        TenantDbContext context,
        IInventoryNotifier notifier,
        ICurrentUserService currentUserService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
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

        var configuredMargin = await _context.Configurations
            .AsNoTracking()
            .Select(c => (decimal?)c.DefaultMarginPercentage)
            .FirstOrDefaultAsync(cancellationToken) ?? 30.00m;

        var effectiveUnitCost = notification.UnitCost > 0m
            ? notification.UnitCost
            : (product.PmpCost > 0m ? product.PmpCost : product.CostPrice);

        product.RegisterStockIn(notification.Quantity, effectiveUnitCost, configuredMargin);

        var resolvedTenantId = Guid.TryParse(notification.TenantId, out var parsedTenantId) && parsedTenantId != Guid.Empty
            ? parsedTenantId
            : (_context.CurrentTenantId ?? _currentUserService.TenantId);

        if (resolvedTenantId != Guid.Empty)
        {
            var kardexTx = KardexTransaction.Create(
                tenantId: resolvedTenantId,
                productId: product.Id,
                movementType: MovementType.In,
                quantity: notification.Quantity,
                unitCost: effectiveUnitCost);

            await _context.KardexTransactions.AddAsync(kardexTx, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _notifier.NotifyStockUpdateAsync(product.Id, product.CurrentStock, cancellationToken);
    }
}
