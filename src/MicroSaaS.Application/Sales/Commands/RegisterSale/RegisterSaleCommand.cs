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
    decimal MontoNeto,
    decimal Iva,
    decimal MontoTotal,
    IReadOnlyList<SaleItemDto> Items,

    // Campos opcionales para ventas rápidas o consumidores finales
    string? RutCliente = null,
    string? RazonSocialCliente = null,

    // Campos para el control de la caja y Mercado Pago
    string PaymentMethod = "CASH",
    decimal CashAmount = 0,
    decimal CardAmount = 0
) : IRequest<Guid>;