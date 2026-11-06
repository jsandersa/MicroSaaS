using MediatR;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Enums;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Products.Commands.UpdateProduct;

/// <summary>
/// Manejador de <see cref="UpdateProductCommand"/> que actualiza el precio de venta de un producto y,
/// si existe una diferencia de stock, exige un justificativo válido de auditoría y registra el movimiento
/// <see cref="MovementType.Adjustment"/> en el libro Kardex.
/// </summary>
public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, bool>
{
    private readonly TenantDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdateProductCommandHandler(
        TenantDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<bool> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (product is null)
        {
            return false;
        }

        // El Costo PMP es contable y solo se recalcula por compras/entradas valorizadas; se preserva el PMP vigente
        var preservedPmp = product.PmpCost > 0m ? product.PmpCost : product.CostPrice;
        var previousStock = product.CurrentStock;
        var stockDelta = request.CurrentStock - previousStock;

        if (stockDelta != 0)
        {
            if (string.IsNullOrWhiteSpace(request.AdjustmentReason) || request.AdjustmentReason.Trim().Length < 5)
            {
                throw new InvalidOperationException(
                    "No se permite ajustar el stock directamente sin un justificativo válido para el Libro Kardex (ej. Merma, Conteo Físico, Daño).");
            }

            var tenantGuid = _context.CurrentTenantId ?? _currentUserService.TenantId;
            if (tenantGuid == Guid.Empty && Guid.TryParse(product.TenantId, out var parsedTenantGuid))
            {
                tenantGuid = parsedTenantGuid;
            }

            if (tenantGuid != Guid.Empty)
            {
                var sign = stockDelta > 0 ? "+" : "-";
                var quantity = Math.Abs(stockDelta);
                var formattedReason = $"[{sign}{quantity} u.] {request.AdjustmentReason.Trim()}";

                var adjustmentKardex = KardexTransaction.Create(
                    tenantId: tenantGuid,
                    productId: product.Id,
                    movementType: MovementType.Adjustment,
                    quantity: quantity,
                    unitCost: preservedPmp,
                    reason: formattedReason);

                await _context.KardexTransactions.AddAsync(adjustmentKardex, cancellationToken);
            }
        }

        product.Update(
            sku: request.Sku,
            name: request.Name,
            salePrice: request.SalePrice,
            costPrice: preservedPmp,
            currentStock: request.CurrentStock);

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
