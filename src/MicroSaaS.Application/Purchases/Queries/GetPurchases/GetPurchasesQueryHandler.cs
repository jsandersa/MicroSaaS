using MediatR;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Purchases.Queries.GetPurchases;

/// <summary>
/// Manejador de <see cref="GetPurchasesQuery"/> que consulta la tabla <c>Purchases</c> con <c>AsNoTracking()</c>,
/// ordena por <c>FechaEmision</c> descendente y proyecta hacia <see cref="PurchaseSummaryDto"/>.
/// </summary>
public class GetPurchasesQueryHandler : IRequestHandler<GetPurchasesQuery, IEnumerable<PurchaseSummaryDto>>
{
    private readonly TenantDbContext _context;

    public GetPurchasesQueryHandler(TenantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IEnumerable<PurchaseSummaryDto>> Handle(
        GetPurchasesQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.Purchases
            .AsNoTracking()
            .OrderByDescending(p => p.FechaEmision)
            .Select(p => new PurchaseSummaryDto(
                p.Id,
                p.Folio,
                p.FechaEmision,
                p.RutProveedor,
                p.RazonSocialProveedor,
                p.MontoTotal))
            .ToListAsync(cancellationToken);
    }
}

