using MediatR;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Products.Commands.CreateProduct;

/// <summary>
/// Manejador del comando <see cref="CreateProductCommand"/> que persiste el producto en el esquema del Tenant autenticado.
/// </summary>
public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Guid>
{
    private readonly TenantDbContext _context;

    public CreateProductCommandHandler(TenantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var product = Product.Create(
            sku: request.Sku,
            name: request.Name,
            price: request.Price,
            stock: request.Stock);

        _context.Products.Add(product);

        await _context.SaveChangesAsync(cancellationToken);

        return product.Id;
    }
}

