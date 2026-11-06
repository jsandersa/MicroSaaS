using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación con EF Core de <see cref="IKardexRepository"/> sobre <see cref="TenantDbContext"/>.
/// </summary>
public sealed class KardexRepository : IKardexRepository
{
    private readonly TenantDbContext _context;

    public KardexRepository(TenantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(
        KardexTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        await _context.KardexTransactions.AddAsync(transaction, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

