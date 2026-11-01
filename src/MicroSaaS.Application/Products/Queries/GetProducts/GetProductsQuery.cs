using MediatR;

namespace MicroSaaS.Application.Products.Queries.GetProducts;

/// <summary>
/// DTO de lectura de un producto del inventario del Tenant actual.
/// </summary>
public record ProductDto(
    Guid Id,
    string Sku,
    string Name,
    decimal SalePrice,
    decimal CostPrice,
    int CurrentStock);

/// <summary>
/// Query para obtener el listado de productos del inventario ordenados alfabéticamente por nombre.
/// </summary>
public record GetProductsQuery() : IRequest<IEnumerable<ProductDto>>;
