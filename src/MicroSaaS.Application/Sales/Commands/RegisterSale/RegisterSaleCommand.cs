using MediatR;
using MicroSaaS.Application.Common.Security;

namespace MicroSaaS.Application.Sales.Commands.RegisterSale;

/// <summary>
/// DTO para cada línea de producto dentro de una venta en el Punto de Venta (POS).
/// </summary>
public sealed record SaleItemDto(
    Guid ProductId = default,
    int Quantity = 0,
    decimal UnitPrice = 0m,
    string NombreItem = "",
    decimal Cantidad = 0m,
    decimal PrecioUnitario = 0m,
    decimal MontoItem = 0m,
    bool EsExento = false,
    string? Sku = null);

/// <summary>
/// Comando CQRS para registrar una venta en el POS, validar la sesión de caja abierta,
/// descontar el inventario en el Kardex y emitir el Documento Tributario Electrónico (DTE).
/// </summary>
[AuthorizeRole("Owner", "Admin")]
public sealed record RegisterSaleCommand(
    IReadOnlyList<SaleItemDto> Items,
    string? PaymentMethod = "CASH",
    int Folio = 0,
    int TipoDte = 39,
    DateTime FechaEmision = default,
    string? RutCliente = null,
    string? RazonSocialCliente = null,
    decimal MontoNeto = 0m,
    decimal Iva = 0m,
    decimal MontoTotal = 0m,
    decimal CashAmount = 0m,
    decimal CardAmount = 0m) : IRequest<Guid>;
