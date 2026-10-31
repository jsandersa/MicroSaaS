using MicroSaaS.Core.Entities.TenantDomain;

namespace MicroSaaS.Core.Interfaces;

/// <summary>
/// Contrato para el servicio de autenticación contra los Web Services del SII (Maullín / Palena).
/// </summary>
public interface ISiiAuthenticationService
{
    Task<string> GetTokenAsync(
        TenantDigitalCertificate certificate,
        string plainTextPassword,
        string environment = "maullin");
}

