using MicroSaaS.Core.Entities.TenantDomain;

namespace MicroSaaS.Core.Interfaces;

/// <summary>
/// Contrato de repositorio para registrar transacciones inmutables en el libro Kardex del Tenant.
/// </summary>
public interface IKardexRepository
{
    Task AddAsync(
        KardexTransaction transaction,
        CancellationToken cancellationToken = default);
}

