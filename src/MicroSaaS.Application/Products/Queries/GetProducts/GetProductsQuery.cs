using MediatR;

namespace MicroSaaS.Application.Products.Queries.GetProducts;

public record ProductDto(
    Guid Id,
    string Sku,
    string Name,
    decimal Price,
    int Stock,
    bool IsActive);

/// <summary>
/// Consulta para obtener todos los productos del esquema del Tenant actual.
/// </summary>
public record GetProductsQuery : IRequest<IReadOnlyList<ProductDto>>;

