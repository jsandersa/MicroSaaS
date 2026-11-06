namespace MicroSaaS.Core.Interfaces;

/// <summary>
/// Servicio para extraer la identidad, el Tenant y el rol del usuario autenticado desde el token JWT actual.
/// </summary>
public interface ICurrentUserService
{
    Guid TenantId { get; }
    Guid UserId { get; }
    string Role { get; }
    bool IsAuthenticated { get; }
}

