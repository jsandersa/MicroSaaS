using MediatR;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Purchases.Commands.RegisterPurchase;

/// <summary>
/// Manejador de <see cref="RegisterPurchaseCommand"/> que persiste una nueva compra y sus líneas de detalle
/// en el esquema aislado del Tenant actual.
/// </summary>
public class RegisterPurchaseCommandHandler : IRequestHandler<RegisterPurchaseCommand, Guid>
{
    private readonly TenantDbContext _context;

    public RegisterPurchaseCommandHandler(TenantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Guid> Handle(RegisterPurchaseCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RutProveedor);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RazonSocialProveedor);
        ArgumentNullException.ThrowIfNull(request.Items);

        var fechaEmisionUtc = request.FechaEmision.Kind switch
        {
            DateTimeKind.Utc => request.FechaEmision,
            DateTimeKind.Local => request.FechaEmision.ToUniversalTime(),
            _ => DateTime.SpecifyKind(request.FechaEmision, DateTimeKind.Utc)
        };

        var purchase = new Purchase
        {
            Id = Guid.NewGuid(),
            TenantId = _context.CurrentTenantId ?? Guid.Empty,
            Folio = request.Folio,
            TipoDte = request.TipoDte,
            FechaEmision = fechaEmisionUtc,
            RutProveedor = request.RutProveedor.Trim(),
            RazonSocialProveedor = request.RazonSocialProveedor.Trim(),
            MontoNeto = request.MontoNeto,
            Iva = request.Iva,
            MontoTotal = request.MontoTotal
        };

        var lineNumber = 1;
        foreach (var itemDto in request.Items)
        {
            var purchaseItem = new PurchaseItem
            {
                Id = Guid.NewGuid(),
                PurchaseId = purchase.Id,
                NumeroLinea = lineNumber++,
                NombreItem = itemDto.NombreItem.Trim(),
                Cantidad = itemDto.Cantidad,
                PrecioUnitario = itemDto.PrecioUnitario,
                MontoItem = itemDto.MontoItem,
                EsExento = itemDto.EsExento
            };

            purchase.AddItem(purchaseItem, itemDto.Sku);
        }

        await _context.Purchases.AddAsync(purchase, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return purchase.Id;
    }
}

