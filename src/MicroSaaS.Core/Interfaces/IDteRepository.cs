using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Enums;

namespace MicroSaaS.Core.Interfaces;

/// <summary>
/// Contrato de repositorio para gestionar la persistencia y el correlativo de folios de <see cref="DteDocument"/>.
/// </summary>
public interface IDteRepository
{
    Task AddAsync(
        DteDocument document,
        CancellationToken cancellationToken = default);

    Task<int> GetNextFolioAsync(
        Guid tenantId,
        DteType dteType,
        CancellationToken cancellationToken = default);

    Task<DteDocument?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DteDocument>> GetByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
