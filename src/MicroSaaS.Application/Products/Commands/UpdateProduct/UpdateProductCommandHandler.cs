using MediatR;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Products.Commands.UpdateProduct;

/// <summary>
/// Manejador de <see cref="UpdateProductCommand"/> que actualiza un producto en el esquema del Tenant autenticado.
/// </summary>
public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, bool>
{
    private readonly TenantDbContext _context;

    public UpdateProductCommandHandler(TenantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<bool> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (product is null)
        {
            return false;
        }

        product.Update(
            sku: request.Sku,
            name: request.Name,
            salePrice: request.SalePrice,
            costPrice: request.CostPrice,
            currentStock: request.CurrentStock);

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
