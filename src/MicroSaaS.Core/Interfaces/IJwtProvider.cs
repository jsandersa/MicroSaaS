using MicroSaaS.Core.Entities;

namespace MicroSaaS.Core.Interfaces;

/// <summary>
/// Contrato para la generación de JSON Web Tokens (JWT) con contexto Multi-Tenant.
/// </summary>
public interface IJwtProvider
{
    string Generate(User user, string schemaName);
}

