using MicroSaaS.Core.Entities.TenantDomain;

namespace MicroSaaS.Core.Interfaces;

/// <summary>
/// Contrato para el servicio de firma electrónica avanzada de documentos XML (DTE) para el SII.
/// </summary>
public interface IDocumentSignatureService
{
    string SignXmlDocument(
        string xmlContent,
        TenantDigitalCertificate certificate,
        string plainTextPassword,
        string? referenceUri = null);
}

