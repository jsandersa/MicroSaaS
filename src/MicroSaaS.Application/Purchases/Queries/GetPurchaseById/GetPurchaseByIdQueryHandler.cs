using MediatR;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Purchases.Queries.GetPurchaseById;

/// <summary>
/// Manejador de <see cref="GetPurchaseByIdQuery"/> que consulta una compra junto con sus ítems
/// mediante <c>AsNoTracking()</c> e <c>Include(p => p.Items)</c>, proyectando hacia <see cref="PurchaseDetailDto"/>.
/// </summary>
public class GetPurchaseByIdQueryHandler : IRequestHandler<GetPurchaseByIdQuery, PurchaseDetailDto?>
{
    private readonly TenantDbContext _context;

    public GetPurchaseByIdQueryHandler(TenantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<PurchaseDetailDto?> Handle(
        GetPurchaseByIdQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.Purchases
            .AsNoTracking()
            .Include(p => p.Items)
            .Where(p => p.Id == request.Id)
            .Select(p => new PurchaseDetailDto(
                p.Id,
                p.Folio,
                p.TipoDte,
                p.RutProveedor,
                p.RazonSocialProveedor,
                p.MontoNeto,
                p.Iva,
                p.MontoTotal,
                p.FechaEmision,
                p.Items
                    .OrderBy(i => i.NumeroLinea)
                    .Select(i => new PurchaseItemDto(
                        i.NombreItem,
                        i.Cantidad,
                        i.PrecioUnitario,
                        i.MontoItem,
                        i.EsExento))))
            .FirstOrDefaultAsync(cancellationToken);
    }
}

