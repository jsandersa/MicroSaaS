using MediatR;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Application.Products.Queries.GetProducts;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Products.Queries.GetProductById;

/// <summary>
/// Manejador de <see cref="GetProductByIdQuery"/> que busca un producto por Id en el esquema del Tenant actual.
/// </summary>
public class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductDto?>
{
    private readonly TenantDbContext _context;

    public GetProductByIdQueryHandler(TenantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ProductDto?> Handle(
        GetProductByIdQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.Products
            .AsNoTracking()
            .Where(p => p.Id == request.Id)
            .Select(p => new ProductDto(
                p.Id,
                p.Sku,
                p.Name,
                p.SalePrice,
                p.CostPrice,
                p.CurrentStock,
                p.PmpCost > 0m ? p.PmpCost : p.CostPrice))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
