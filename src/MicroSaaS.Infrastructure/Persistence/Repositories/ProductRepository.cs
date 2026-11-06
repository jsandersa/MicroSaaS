using Microsoft.EntityFrameworkCore;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación con EF Core de <see cref="IProductRepository"/> sobre <see cref="TenantDbContext"/>.
/// </summary>
public sealed class ProductRepository : IProductRepository
{
    private readonly TenantDbContext _context;

    public ProductRepository(TenantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Product?> GetByIdAsync(
        Guid id,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenantIdString = tenantId.ToString();

        return await _context.Products
            .FirstOrDefaultAsync(
                p => p.Id == id && (p.TenantId == tenantIdString || string.IsNullOrEmpty(p.TenantId)),
                cancellationToken);
    }

    public async Task UpdateAsync(
        Product product,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);

        _context.Products.Update(product);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

