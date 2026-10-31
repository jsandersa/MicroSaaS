using MediatR;
using Microsoft.AspNetCore.Http;

namespace MicroSaaS.Application.Certificates.Commands.UploadCertificate;

/// <summary>
/// Comando para cargar y almacenar de forma segura el certificado digital (.pfx) de facturación electrónica SII del Tenant actual.
/// </summary>
public record UploadCertificateCommand(
    IFormFile File,
    string Password) : IRequest<Guid>;

