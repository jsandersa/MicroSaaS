using MediatR;

namespace MicroSaaS.Application.Dashboard.Queries.GetDashboardMetrics;

/// <summary>
/// Query para obtener las métricas comerciales y la proyección de IVA del mes en curso para el Tenant actual.
/// </summary>
public record GetDashboardMetricsQuery() : IRequest<DashboardResultDto>;

