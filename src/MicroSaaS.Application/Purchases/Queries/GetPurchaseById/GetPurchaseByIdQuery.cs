using MediatR;

namespace MicroSaaS.Application.Purchases.Queries.GetPurchaseById;

/// <summary>
/// DTO que representa una línea de detalle de una compra consultada por su identificador.
/// </summary>
public record PurchaseItemDto(
    string NombreItem,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal MontoItem,
    bool EsExento);

/// <summary>
/// DTO con la cabecera completa y las líneas de detalle de una compra comercial.
/// </summary>
public record PurchaseDetailDto(
    Guid Id,
    int Folio,
    int TipoDte,
    string RutProveedor,
    string RazonSocialProveedor,
    decimal MontoNeto,
    decimal Iva,
    decimal MontoTotal,
    DateTime FechaEmision,
    IEnumerable<PurchaseItemDto> Items);

/// <summary>
/// Query para obtener el detalle completo de una compra comercial por su identificador único.
/// </summary>
public record GetPurchaseByIdQuery(Guid Id) : IRequest<PurchaseDetailDto?>;

