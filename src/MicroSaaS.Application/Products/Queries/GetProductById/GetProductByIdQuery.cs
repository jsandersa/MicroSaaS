using MediatR;
using MicroSaaS.Application.Products.Queries.GetProducts;

namespace MicroSaaS.Application.Products.Queries.GetProductById;

/// <summary>
/// Consulta para obtener un producto por su identificador dentro del esquema del Tenant actual.
/// </summary>
public record GetProductByIdQuery(Guid Id) : IRequest<ProductDto?>;

