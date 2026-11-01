using MediatR;

namespace MicroSaaS.Application.Sales.Queries.GetSales;

/// <summary>
/// DTO de resumen de una venta comercial para listados e historiales.
/// </summary>
public record SaleSummaryDto(
    Guid Id,
    int Folio,
    DateTime FechaEmision,
    string RutCliente,
    string RazonSocialCliente,
    decimal MontoTotal);

/// <summary>
/// Query para obtener el listado de ventas del Tenant actual ordenadas por fecha de emisión descendente.
/// </summary>
public record GetSalesQuery() : IRequest<IEnumerable<SaleSummaryDto>>;

