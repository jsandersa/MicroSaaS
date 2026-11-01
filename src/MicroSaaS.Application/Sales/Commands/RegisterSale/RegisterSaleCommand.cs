using MediatR;

namespace MicroSaaS.Application.Sales.Commands.RegisterSale;

/// <summary>
/// DTO para cada línea de detalle al registrar una nueva venta comercial.
/// </summary>
public record SaleItemDto(
    string NombreItem,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal MontoItem,
    bool EsExento = false,
    string? Sku = null);

/// <summary>
/// Comando para registrar una nueva venta comercial (<see cref="MicroSaaS.Core.Entities.TenantDomain.Sale"/>) en el esquema del Tenant actual.
/// </summary>
public record RegisterSaleCommand(
    int Folio,
    int TipoDte,
    DateTime FechaEmision,
    string RutCliente,
    string RazonSocialCliente,
    decimal MontoNeto,
    decimal Iva,
    decimal MontoTotal,
    IReadOnlyList<SaleItemDto> Items) : IRequest<Guid>;

