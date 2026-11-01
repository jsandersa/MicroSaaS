using MediatR;

namespace MicroSaaS.Application.Purchases.Commands.RegisterPurchase;

/// <summary>
/// DTO para cada línea de detalle al registrar una nueva compra comercial.
/// </summary>
public record PurchaseItemDto(
    string NombreItem,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal MontoItem,
    bool EsExento = false,
    string? Sku = null);

/// <summary>
/// Comando para registrar una nueva compra comercial (<see cref="MicroSaaS.Core.Entities.TenantDomain.Purchase"/>) en el esquema del Tenant actual.
/// </summary>
public record RegisterPurchaseCommand(
    int Folio,
    int TipoDte,
    DateTime FechaEmision,
    string RutProveedor,
    string RazonSocialProveedor,
    decimal MontoNeto,
    decimal Iva,
    decimal MontoTotal,
    IReadOnlyList<PurchaseItemDto> Items) : IRequest<Guid>;

