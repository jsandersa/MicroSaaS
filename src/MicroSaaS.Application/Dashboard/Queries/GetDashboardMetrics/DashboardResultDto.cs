namespace MicroSaaS.Application.Dashboard.Queries.GetDashboardMetrics;

/// <summary>
/// DTO con las métricas comerciales y tributarias consolidadas del Tenant para el Dashboard principal.
/// </summary>
public record DashboardResultDto(
    decimal VentasDelDia,
    decimal VentasDelMes,
    decimal ComprasDelMes,
    int DocumentosEmitidosMes,
    TaxProjectionDto ProyeccionIva);

/// <summary>
/// Proyección mensual de IVA (Débito Fiscal por ventas vs. Crédito Fiscal por compras).
/// </summary>
public record TaxProjectionDto(
    decimal IvaDebitoVentas,
    decimal IvaCreditoCompras,
    decimal IvaPagarEstimado);

