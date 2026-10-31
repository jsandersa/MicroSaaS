namespace MicroSaaS.Core.Interfaces;

/// <summary>
/// Proveedor del contexto del Tenant actual para resolver el esquema de base de datos por petición (Schema-per-tenant).
/// </summary>
public interface ITenantProvider
{
    /// <summary>
    /// Obtiene el nombre del esquema de PostgreSQL asociado al Tenant de la petición actual.
    /// </summary>
    string GetCurrentSchema();

    /// <summary>
    /// Establece el nombre del esquema de PostgreSQL para la petición actual.
    /// </summary>
    void SetCurrentSchema(string schema);

    /// <summary>
    /// Obtiene el identificador único del Tenant de la petición actual, si fue resuelto desde el JWT o contexto.
    /// </summary>
    Guid? GetCurrentTenantId();

    /// <summary>
    /// Establece el identificador único del Tenant para la petición actual.
    /// </summary>
    void SetCurrentTenantId(Guid? tenantId);
}

