using MediatR;

namespace MicroSaaS.Application.Products.Commands.UpdateProduct;

/// <summary>
/// Comando para actualizar un producto existente dentro del esquema del Tenant actual.
/// </summary>
public record UpdateProductCommand(
    Guid Id,
    string Sku,
    string Name,
    decimal SalePrice,
    decimal CostPrice,
    int CurrentStock,
    string? AdjustmentReason = null) : IRequest<bool>;
