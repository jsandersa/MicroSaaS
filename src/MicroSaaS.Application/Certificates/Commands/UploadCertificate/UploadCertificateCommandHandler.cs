using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Certificates.Commands.UploadCertificate;

/// <summary>
/// Manejador de <see cref="UploadCertificateCommand"/> que valida el certificado X.509 (.pfx) en memoria,
/// extrae sus metadatos (<c>Subject</c>, <c>NotAfter</c>), cifra la contraseña con AES-256
/// y persiste el nuevo certificado activo en el esquema aislado del Tenant.
/// </summary>
public class UploadCertificateCommandHandler : IRequestHandler<UploadCertificateCommand, Guid>
{
    private readonly TenantDbContext _tenantDbContext;
    private readonly IEncryptionService _encryptionService;

    public UploadCertificateCommandHandler(
        TenantDbContext tenantDbContext,
        IEncryptionService encryptionService)
    {
        _tenantDbContext = tenantDbContext ?? throw new ArgumentNullException(nameof(tenantDbContext));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
    }

    public async Task<Guid> Handle(UploadCertificateCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.File);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Password);

        if (request.File.Length == 0)
        {
            throw new ArgumentException("El archivo del certificado digital (.pfx) está vacío.", nameof(request.File));
        }

        // 1. Leer los bytes del archivo .pfx en memoria
        using var memoryStream = new MemoryStream();
        await request.File.CopyToAsync(memoryStream, cancellationToken);
        var fileBytes = memoryStream.ToArray();

        string subject;
        DateTime expirationDate;

        // 2. Instanciar el certificado en memoria para validar que la contraseña sea correcta y extraer metadatos
        try
        {
#pragma warning disable SYSLIB0057
            using var x509 = new X509Certificate2(
                fileBytes,
                request.Password,
                X509KeyStorageFlags.DefaultKeySet);
#pragma warning restore SYSLIB0057

            subject = x509.Subject;
            expirationDate = x509.NotAfter.ToUniversalTime();
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                "El certificado digital (.pfx) es inválido o la contraseña proporcionada es incorrecta.",
                ex);
        }

        // 3. Cifrar la contraseña en texto plano mediante AES-256 (IEncryptionService)
        var encryptedPassword = _encryptionService.Encrypt(request.Password);

        // 4. Si ya existe un certificado activo previo en el esquema del Tenant, marcarlo como inactivo
        var activeCertificates = await _tenantDbContext.DigitalCertificates
            .Where(c => c.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var existing in activeCertificates)
        {
            existing.Deactivate();
        }

        // 5. Instanciar y persistir la nueva entidad TenantDigitalCertificate
        var certificate = TenantDigitalCertificate.Create(
            fileName: request.File.FileName,
            fileData: fileBytes,
            encryptedPassword: encryptedPassword,
            expirationDate: expirationDate,
            subject: subject);

        await _tenantDbContext.DigitalCertificates.AddAsync(certificate, cancellationToken);
        await _tenantDbContext.SaveChangesAsync(cancellationToken);

        return certificate.Id;
    }
}

