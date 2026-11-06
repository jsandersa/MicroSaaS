using MicroSaaS.Core.Entities.TenantDomain;

namespace MicroSaaS.Core.Interfaces;

/// <summary>
/// Contrato de repositorio para obtener y actualizar productos dentro del esquema aislado del Tenant.
/// </summary>
public interface IProductRepository
{
    Task<Product?> GetByIdAsync(
        Guid id,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Product product,
        CancellationToken cancellationToken = default);
}

