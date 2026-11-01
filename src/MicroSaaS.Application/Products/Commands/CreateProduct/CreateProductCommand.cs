using MediatR;

namespace MicroSaaS.Application.Products.Commands.CreateProduct;

/// <summary>
/// Comando para registrar un nuevo producto en el inventario privado del Tenant actual.
/// </summary>
public record CreateProductCommand(
    string Sku,
    string Name,
    decimal SalePrice,
    decimal CostPrice,
    int CurrentStock) : IRequest<Guid>;
