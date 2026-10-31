using MediatR;

namespace MicroSaaS.Application.Products.Commands.DeleteProduct;

/// <summary>
/// Comando para eliminar un producto del inventario del Tenant actual.
/// </summary>
public record DeleteProductCommand(Guid Id) : IRequest<bool>;

