using MediatR;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Dashboard.Queries.GetDashboardMetrics;

/// <summary>
/// Manejador de <see cref="GetDashboardMetricsQuery"/> que consulta las tablas <c>Sales</c> y <c>Purchases</c>
/// del esquema del Tenant actual para calcular ventas del día, ventas/compras del mes y la proyección de IVA.
/// </summary>
public class GetDashboardMetricsQueryHandler
    : IRequestHandler<GetDashboardMetricsQuery, DashboardResultDto>
{
    private readonly TenantDbContext _tenantDbContext;

    public GetDashboardMetricsQueryHandler(TenantDbContext tenantDbContext)
    {
        _tenantDbContext = tenantDbContext ?? throw new ArgumentNullException(nameof(tenantDbContext));
    }

    public async Task<DashboardResultDto> Handle(
        GetDashboardMetricsQuery request,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var startOfDayUtc = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 0, 0, 0, DateTimeKind.Utc);
        var startOfNextDayUtc = startOfDayUtc.AddDays(1);

        var startOfMonthUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startOfNextMonthUtc = startOfMonthUtc.AddMonths(1);

        // 1. Consultar ventas del mes actual
        var monthlySales = await _tenantDbContext.Sales
            .AsNoTracking()
            .Where(s => s.FechaEmision >= startOfMonthUtc && s.FechaEmision < startOfNextMonthUtc)
            .Select(s => new
            {
                s.FechaEmision,
                s.MontoTotal,
                s.Iva
            })
            .ToListAsync(cancellationToken);

        // 2. Consultar compras del mes actual
        var monthlyPurchases = await _tenantDbContext.Purchases
            .AsNoTracking()
            .Where(p => p.FechaEmision >= startOfMonthUtc && p.FechaEmision < startOfNextMonthUtc)
            .Select(p => new
            {
                p.MontoTotal,
                p.Iva
            })
            .ToListAsync(cancellationToken);

        // 3. Calcular agregaciones comerciales y tributarias
        var ventasDelDia = monthlySales
            .Where(s => s.FechaEmision >= startOfDayUtc && s.FechaEmision < startOfNextDayUtc)
            .Sum(s => s.MontoTotal);

        var ventasDelMes = monthlySales.Sum(s => s.MontoTotal);
        var comprasDelMes = monthlyPurchases.Sum(p => p.MontoTotal);
        var documentosEmitidosMes = monthlySales.Count;

        var ivaDebitoVentas = monthlySales.Sum(s => s.Iva);
        var ivaCreditoCompras = monthlyPurchases.Sum(p => p.Iva);
        var ivaPagarEstimado = Math.Max(0m, ivaDebitoVentas - ivaCreditoCompras);

        var proyeccionIva = new TaxProjectionDto(
            IvaDebitoVentas: ivaDebitoVentas,
            IvaCreditoCompras: ivaCreditoCompras,
            IvaPagarEstimado: ivaPagarEstimado);

        return new DashboardResultDto(
            VentasDelDia: ventasDelDia,
            VentasDelMes: ventasDelMes,
            ComprasDelMes: comprasDelMes,
            DocumentosEmitidosMes: documentosEmitidosMes,
            ProyeccionIva: proyeccionIva);
    }
}

