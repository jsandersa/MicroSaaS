using Microsoft.EntityFrameworkCore;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación con EF Core de <see cref="ISaleRepository"/> sobre <see cref="TenantDbContext"/>.
/// </summary>
public sealed class SaleRepository : ISaleRepository
{
    private readonly TenantDbContext _context;

    public SaleRepository(TenantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(
        Sale sale,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sale);

        await _context.Sales.AddAsync(sale, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        Sale sale,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sale);

        _context.Sales.Update(sale);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Sale>> GetSalesByCashRegisterSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .AsNoTracking()
            .Where(s => s.CashRegisterSessionId == sessionId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
