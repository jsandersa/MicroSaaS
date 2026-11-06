using MicroSaaS.Core.Entities.TenantDomain;

namespace MicroSaaS.Core.Interfaces;

/// <summary>
/// Contrato de repositorio para persistir y consultar ventas (<see cref="Sale"/>) dentro del esquema aislado del Tenant.
/// </summary>
public interface ISaleRepository
{
    Task AddAsync(
        Sale sale,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Sale sale,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Sale>> GetSalesByCashRegisterSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
}
