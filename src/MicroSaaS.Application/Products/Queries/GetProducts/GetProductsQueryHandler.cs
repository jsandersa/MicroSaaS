using MediatR;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Products.Queries.GetProducts;

/// <summary>
/// Manejador de <see cref="GetProductsQuery"/> que consulta únicamente los productos del esquema del Tenant actual
/// a través de <see cref="TenantDbContext"/> y <see cref="MicroSaaS.Infrastructure.Interceptors.SchemaInterceptor"/>.
/// </summary>
public class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, IReadOnlyList<ProductDto>>
{
    private readonly TenantDbContext _context;

    public GetProductsQueryHandler(TenantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<ProductDto>> Handle(
        GetProductsQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.Products
            .AsNoTracking()
            .OrderBy(p => p.Sku)
            .Select(p => new ProductDto(
                p.Id,
                p.Sku,
                p.Name,
                p.Price,
                p.Stock,
                p.IsActive))
            .ToListAsync(cancellationToken);
    }
}

