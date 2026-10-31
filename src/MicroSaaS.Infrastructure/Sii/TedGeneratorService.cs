using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using MicroSaaS.Core.Entities.TenantDomain.Dte;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Infrastructure.Sii;

/// <summary>
/// Servicio criptográfico encargado de construir y firmar el Timbre Electrónico de Datos (<c>&lt;TED&gt;</c>)
/// de un DTE utilizando los parámetros RSA del CAF (<c>&lt;RSAPrivKey&gt;</c>) con SHA1withRSA (PKCS#1).
/// </summary>
public class TedGeneratorService : ITedGeneratorService
{
    private static readonly XNamespace SiiNs = SiiDteBuilderService.SiiDteNamespaceUri;

    public XElement GenerateTed(DteDocument document, CafDocument caf)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Header);
        ArgumentNullException.ThrowIfNull(document.Items);
        ArgumentNullException.ThrowIfNull(caf);
        ArgumentNullException.ThrowIfNull(caf.Caf);
        ArgumentNullException.ThrowIfNull(caf.RsaPrivateKey);

        if (document.Items.Count == 0)
        {
            throw new ArgumentException("El DTE debe contener al menos un ítem para generar el nodo <IT1> del TED.", nameof(document));
        }

        var header = document.Header;
        var rango = caf.Caf.DatosAutorizacion?.RangoFolios;
        if (rango is not null && (rango.Desde > 0 || rango.Hasta > 0))
        {
            if (header.Folio < rango.Desde || header.Folio > rango.Hasta)
            {
                throw new InvalidOperationException(
                    $"El folio {header.Folio} está fuera del rango autorizado en el CAF ({rango.Desde} - {rango.Hasta}).");
            }
        }

        var firstItem = document.Items
            .OrderBy(i => i.NumeroLinea)
            .First();

        // 1. Construir el nodo <DD> (Datos del Documento) respetando estrictamente el orden de nodos exigido:
        //    RE, RS, TD, F, FE, RR, RSR, MNT, IT1 (+ <CAF> y <TSTED>)
        var ddElement = new XElement(SiiNs + "DD",
            new XElement(SiiNs + "RE", NormalizeRut(header.Emisor.RutEmisor)),
            new XElement(SiiNs + "RS", Truncate(header.Emisor.RazonSocial.Trim(), 40)),
            new XElement(SiiNs + "TD", header.TipoDte),
            new XElement(SiiNs + "F", header.Folio),
            new XElement(SiiNs + "FE", header.FechaEmision.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new XElement(SiiNs + "RR", NormalizeRut(header.Receptor.RutReceptor)),
            new XElement(SiiNs + "RSR", Truncate(header.Receptor.RazonSocialReceptor.Trim(), 40)),
            new XElement(SiiNs + "MNT", Math.Round(header.MontoTotal, 0, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)),
            new XElement(SiiNs + "IT1", Truncate(firstItem.NombreItem.Trim(), 40)),
            BuildCafElement(caf.Caf),
            new XElement(SiiNs + "TSTED", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture))
        );

        // Obtener el string plano de <DD> sin saltos de línea ni indentación para aplicar la firma RSA
        var ddRawXml = BuildCanonicalDdString(ddElement);

        // 2. Extraer parámetros RSA desde <RSAPrivKey> (Base64) e importarlos en RSA.Create()
        var rsaParameters = ExtractRsaParameters(caf.RsaPrivateKey);

        using var rsa = RSA.Create();
        rsa.ImportParameters(rsaParameters);

        // 3. Firmar el string del <DD> utilizando SHA1 con padding PKCS#1
        var ddBytes = Encoding.Latin1.GetBytes(ddRawXml);
        var signatureBytes = rsa.SignData(ddBytes, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
        var base64Signature = Convert.ToBase64String(signatureBytes);

        // 4. Construir y retornar el nodo <TED version="1.0"> con <DD> y <FRMA algoritmo="SHA1withRSA">
        return new XElement(SiiNs + "TED",
            new XAttribute("version", "1.0"),
            ddElement,
            new XElement(SiiNs + "FRMA",
                new XAttribute("algoritmo", "SHA1withRSA"),
                base64Signature)
        );
    }

    private static XElement BuildCafElement(CafNode cafNode)
    {
        var da = cafNode.DatosAutorizacion;
        var daElement = new XElement(SiiNs + "DA",
            new XElement(SiiNs + "RE", NormalizeRut(da.RutEmisor)),
            new XElement(SiiNs + "RS", da.RazonSocial.Trim()),
            new XElement(SiiNs + "TD", da.TipoDte),
            new XElement(SiiNs + "RNG",
                new XElement(SiiNs + "D", da.RangoFolios.Desde),
                new XElement(SiiNs + "H", da.RangoFolios.Hasta)
            ),
            new XElement(SiiNs + "FA", da.FechaAutorizacion.Trim())
        );

        if (da.ClavePublicaRsa is not null &&
            (!string.IsNullOrWhiteSpace(da.ClavePublicaRsa.Modulo) || !string.IsNullOrWhiteSpace(da.ClavePublicaRsa.Exponente)))
        {
            daElement.Add(new XElement(SiiNs + "RSAPK",
                new XElement(SiiNs + "M", da.ClavePublicaRsa.Modulo.Trim()),
                new XElement(SiiNs + "E", da.ClavePublicaRsa.Exponente.Trim())
            ));
        }

        if (da.IdKey.HasValue)
        {
            daElement.Add(new XElement(SiiNs + "IDK", da.IdKey.Value));
        }

        return new XElement(SiiNs + "CAF",
            new XAttribute("version", string.IsNullOrWhiteSpace(cafNode.Version) ? "1.0" : cafNode.Version),
            daElement,
            new XElement(SiiNs + "FRMA",
                new XAttribute("algoritmo", string.IsNullOrWhiteSpace(cafNode.FirmaSii?.Algoritmo) ? "SHA1withRSA" : cafNode.FirmaSii.Algoritmo),
                cafNode.FirmaSii?.Value?.Trim() ?? string.Empty)
        );
    }

    private static RSAParameters ExtractRsaParameters(RsaPrivateKeyNode keyNode)
    {
        return new RSAParameters
        {
            Modulus = DecodeBase64Parameter(keyNode.Modulus, nameof(keyNode.Modulus)),
            Exponent = DecodeBase64Parameter(keyNode.Exponent, nameof(keyNode.Exponent)),
            D = DecodeBase64Parameter(keyNode.D, nameof(keyNode.D)),
            P = DecodeBase64Parameter(keyNode.P, nameof(keyNode.P)),
            Q = DecodeBase64Parameter(keyNode.Q, nameof(keyNode.Q)),
            DP = DecodeBase64Parameter(keyNode.DP, nameof(keyNode.DP)),
            DQ = DecodeBase64Parameter(keyNode.DQ, nameof(keyNode.DQ)),
            InverseQ = DecodeBase64Parameter(keyNode.InverseQ, nameof(keyNode.InverseQ))
        };
    }

    private static byte[] DecodeBase64Parameter(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"El parámetro RSA '{paramName}' en <RSAPrivKey> del CAF no puede estar vacío.", paramName);
        }

        return Convert.FromBase64String(value.Trim());
    }

    private static string BuildCanonicalDdString(XElement ddElement)
    {
        // Clonamos sin declaración xmlns redundante en el nodo <DD> para el cálculo de firma plana
        var cleanDd = StripNamespaces(ddElement);
        return cleanDd.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement StripNamespaces(XElement element)
    {
        return new XElement(
            element.Name.LocalName,
            element.Attributes().Where(a => !a.IsNamespaceDeclaration),
            element.Nodes().Select(n => n is XElement child ? StripNamespaces(child) : n));
    }

    private static string NormalizeRut(string rut)
    {
        if (string.IsNullOrWhiteSpace(rut))
        {
            return string.Empty;
        }

        return rut.Replace(".", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}

