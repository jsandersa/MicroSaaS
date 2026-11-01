using MediatR;

namespace MicroSaaS.Application.Purchases.Queries.GetPurchases;

/// <summary>
/// DTO de resumen de una compra comercial para listados e historiales.
/// </summary>
public record PurchaseSummaryDto(
    Guid Id,
    int Folio,
    DateTime FechaEmision,
    string RutProveedor,
    string RazonSocialProveedor,
    decimal MontoTotal);

/// <summary>
/// Query para obtener el listado de compras del Tenant actual ordenadas por fecha de emisión descendente.
/// </summary>
public record GetPurchasesQuery() : IRequest<IEnumerable<PurchaseSummaryDto>>;

