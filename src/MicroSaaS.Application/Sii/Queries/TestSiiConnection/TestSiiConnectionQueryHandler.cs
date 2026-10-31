using MediatR;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Sii.Queries.TestSiiConnection;

/// <summary>
/// Manejador de <see cref="TestSiiConnectionQuery"/> que obtiene el certificado activo del Tenant,
/// descifra su contraseña con AES-256 y solicita un Token de sesión a Maullín (SII).
/// </summary>
public class TestSiiConnectionQueryHandler
    : IRequestHandler<TestSiiConnectionQuery, SiiConnectionResultDto>
{
    private const string DefaultSiiEnvironment = "maullin";

    private readonly TenantDbContext _tenantDbContext;
    private readonly IEncryptionService _encryptionService;
    private readonly ISiiAuthenticationService _siiAuthenticationService;

    public TestSiiConnectionQueryHandler(
        TenantDbContext tenantDbContext,
        IEncryptionService encryptionService,
        ISiiAuthenticationService siiAuthenticationService)
    {
        _tenantDbContext = tenantDbContext ?? throw new ArgumentNullException(nameof(tenantDbContext));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _siiAuthenticationService = siiAuthenticationService ?? throw new ArgumentNullException(nameof(siiAuthenticationService));
    }

    public async Task<SiiConnectionResultDto> Handle(
        TestSiiConnectionQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Obtener el certificado digital activo del esquema del Tenant actual
        var certificate = await _tenantDbContext.DigitalCertificates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IsActive, cancellationToken);

        if (certificate is null)
        {
            return new SiiConnectionResultDto(
                IsSuccess: false,
                Token: null,
                ErrorMessage: "No se encontró ningún certificado digital (.pfx) activo en la bóveda del Tenant.",
                Environment: DefaultSiiEnvironment,
                CertificateSubject: null);
        }

        try
        {
            // 2. Descifrar la contraseña del certificado usando IEncryptionService (AES-256)
            var decryptedPassword = _encryptionService.Decrypt(certificate.EncryptedPassword);

            // 3. Solicitar Token de sesión al entorno de certificación del SII ("maullin")
            var token = await _siiAuthenticationService.GetTokenAsync(
                certificate,
                decryptedPassword,
                DefaultSiiEnvironment);

            return new SiiConnectionResultDto(
                IsSuccess: true,
                Token: token,
                ErrorMessage: null,
                Environment: DefaultSiiEnvironment,
                CertificateSubject: certificate.Subject);
        }
        catch (Exception ex)
        {
            // 4. Capturar rechazo del SII o error de red y retornar la Glosa/mensaje exacto
            return new SiiConnectionResultDto(
                IsSuccess: false,
                Token: null,
                ErrorMessage: ex.Message,
                Environment: DefaultSiiEnvironment,
                CertificateSubject: certificate.Subject);
        }
    }
}

