using Microsoft.EntityFrameworkCore;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Enums;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación con EF Core de <see cref="IDteRepository"/> sobre <see cref="TenantDbContext"/>.
/// </summary>
public sealed class DteRepository : IDteRepository
{
    private readonly TenantDbContext _context;

    public DteRepository(TenantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(
        DteDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var activeCaf = await _context.CafAuthorizations
            .Where(c => c.IsActive && c.DteType == (int)document.DteType && c.StartFolio <= document.Folio && c.EndFolio >= document.Folio)
            .OrderByDescending(c => c.AuthorizedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeCaf is not null)
        {
            activeCaf.UpdateCurrentFolio(document.Folio);
        }

        await _context.DteDocuments.AddAsync(document, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetNextFolioAsync(
        Guid tenantId,
        DteType dteType,
        CancellationToken cancellationToken = default)
    {
        var maxFolio = await _context.DteDocuments
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.DteType == dteType)
            .Select(d => (int?)d.Folio)
            .MaxAsync(cancellationToken);

        var activeCaf = await _context.CafAuthorizations
            .AsNoTracking()
            .Where(c => c.IsActive && c.DteType == (int)dteType)
            .OrderByDescending(c => c.AuthorizedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeCaf is not null)
        {
            var nextCandidate = Math.Max((maxFolio ?? 0) + 1, activeCaf.StartFolio);
            if (nextCandidate > activeCaf.EndFolio)
            {
                throw new InvalidOperationException(
                    $"Se agotaron los folios CAF autorizados para el DTE Tipo {(int)dteType} (Rango {activeCaf.StartFolio}-{activeCaf.EndFolio}). Por favor cargue un nuevo CAF en Configuración.");
            }

            return nextCandidate;
        }

        return (maxFolio ?? 0) + 1;
    }

    public async Task<DteDocument?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.DteDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<DteDocument>> GetByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DteDocuments
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .OrderByDescending(d => d.EmissionDate)
            .ToListAsync(cancellationToken);
    }
}
