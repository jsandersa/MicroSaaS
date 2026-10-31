using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Infrastructure.Sii;

/// <summary>
/// Servicio de firma electrónica XML-DSig para Documentos Tributarios Electrónicos (DTE) del SII.
/// </summary>
public class SiiSignatureService : IDocumentSignatureService
{
    public string SignXmlDocument(
        string xmlContent,
        TenantDigitalCertificate certificate,
        string plainTextPassword,
        string? referenceUri = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xmlContent);
        ArgumentNullException.ThrowIfNull(certificate);
        ArgumentException.ThrowIfNullOrWhiteSpace(plainTextPassword);

        // 1. Cargar el documento XML preservando espacios en blanco (requerido por el SII para no invalidar el digest)
        var xmlDocument = new XmlDocument
        {
            PreserveWhitespace = true
        };
        xmlDocument.LoadXml(xmlContent);

        if (xmlDocument.DocumentElement is null)
        {
            throw new InvalidOperationException("El documento XML proporcionado no contiene un nodo raíz válido.");
        }

        // 2. Instanciar el certificado digital X.509 con flag Exportable y extraer su llave privada RSA
#pragma warning disable SYSLIB0057
        using var x509Certificate = new X509Certificate2(
            certificate.FileData,
            plainTextPassword,
            X509KeyStorageFlags.Exportable);
#pragma warning restore SYSLIB0057

        using var rsaPrivateKey = x509Certificate.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("El certificado digital no contiene una clave privada RSA válida para firmar DTE.");

        // 3. Configurar SignedXml con la llave privada RSA del contribuyente
        var signedXml = new SignedXml(xmlDocument)
        {
            SigningKey = rsaPrivateKey
        };

        // Si no se especifica referenceUri explícitamente, buscar nodo <Documento ID="..."> o firmar documento raíz ("")
        var resolvedReferenceUri = referenceUri;
        if (string.IsNullOrWhiteSpace(resolvedReferenceUri))
        {
            var documentoNode = xmlDocument.SelectSingleNode("//*[@ID]") as XmlElement;
            resolvedReferenceUri = documentoNode is not null
                ? $"#{documentoNode.GetAttribute("ID")}"
                : string.Empty;
        }

        var reference = new Reference(resolvedReferenceUri);
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        signedXml.AddReference(reference);

        // 4. Construir KeyInfo con la llave pública RSA y los datos X509 del certificado (KeyInfoX509Data) para validación del SII
        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new RSAKeyValue(rsaPrivateKey));
        keyInfo.AddClause(new KeyInfoX509Data(x509Certificate));
        signedXml.KeyInfo = keyInfo;

        // 5. Calcular la firma digital e inyectar el nodo <Signature> en el documento XML
        signedXml.ComputeSignature();
        XmlElement xmlDigitalSignature = signedXml.GetXml();

        xmlDocument.DocumentElement.AppendChild(
            xmlDocument.ImportNode(xmlDigitalSignature, deep: true));

        return xmlDocument.OuterXml;
    }
}

