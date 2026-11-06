using MediatR;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Sales.Queries.GetSales;

/// <summary>
/// Manejador de <see cref="GetSalesQuery"/> que consulta la tabla <c>Sales</c> con <c>AsNoTracking()</c>,
/// ordena por <c>FechaEmision</c> descendente y proyecta hacia <see cref="SaleSummaryDto"/>.
/// </summary>
public class GetSalesQueryHandler : IRequestHandler<GetSalesQuery, IEnumerable<SaleSummaryDto>>
{
    private readonly TenantDbContext _context;

    public GetSalesQueryHandler(TenantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IEnumerable<SaleSummaryDto>> Handle(
        GetSalesQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.Sales
            .AsNoTracking()
            .OrderByDescending(s => s.FechaEmision)
            .Select(s => new SaleSummaryDto(
                s.Id,
                s.Folio,
                s.FechaEmision,
                s.RutCliente ?? string.Empty,
                s.RazonSocialCliente ?? string.Empty,
                s.MontoTotal))
            .ToListAsync(cancellationToken);
    }
}
