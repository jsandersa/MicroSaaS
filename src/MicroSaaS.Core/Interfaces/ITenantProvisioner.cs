namespace MicroSaaS.Core.Interfaces;

/// <summary>
/// Contrato para el servicio de aprovisionamiento dinámico de esquemas y migraciones por Tenant (Schema-per-Tenant).
/// </summary>
public interface ITenantProvisioner
{
    Task ProvisionTenantAsync(string schemaName, CancellationToken cancellationToken = default);
}

