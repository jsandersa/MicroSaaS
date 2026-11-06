using MediatR;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Products.Queries.GetProducts;

/// <summary>
/// Manejador de <see cref="GetProductsQuery"/> que consulta la tabla <c>Products</c> con <c>AsNoTracking()</c>,
/// ordena alfabéticamente por <c>Name</c> y proyecta hacia <see cref="ProductDto"/>.
/// </summary>
public class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, IEnumerable<ProductDto>>
{
    private readonly TenantDbContext _context;

    public GetProductsQueryHandler(TenantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IEnumerable<ProductDto>> Handle(
        GetProductsQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new ProductDto(
                p.Id,
                p.Sku,
                p.Name,
                p.SalePrice,
                p.CostPrice,
                p.CurrentStock,
                p.PmpCost > 0m ? p.PmpCost : p.CostPrice))
            .ToListAsync(cancellationToken);
    }
}
