using System.Xml.Serialization;

namespace MicroSaaS.Core.Entities.TenantDomain.Dte;

/// <summary>
/// Representa el archivo XML de Código de Autorización de Folios (CAF) entregado por el SII (<c>&lt;AUTORIZACION&gt;</c>),
/// incluyendo los rangos de folios autorizados y la clave privada RSA (<c>&lt;RSAPrivKey&gt;</c>) para timbrar el TED.
/// </summary>
[XmlRoot("AUTORIZACION")]
public class CafDocument
{
    [XmlElement("CAF")]
    public CafNode Caf { get; set; } = new();

    [XmlElement("RSAPrivKey")]
    public RsaPrivateKeyNode RsaPrivateKey { get; set; } = new();

    /// <summary>
    /// Deserializa un documento CAF desde su representación XML en texto plano.
    /// </summary>
    public static CafDocument FromXml(string xmlContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xmlContent);

        var serializer = new XmlSerializer(typeof(CafDocument));
        using var reader = new StringReader(xmlContent);
        return (CafDocument?)serializer.Deserialize(reader)
            ?? throw new InvalidOperationException("No fue posible deserializar el archivo XML del CAF (<AUTORIZACION>).");
    }
}

/// <summary>
/// Nodo <c>&lt;CAF&gt;</c> que contiene los datos de autorización (<c>&lt;DA&gt;</c>) y la firma del SII (<c>&lt;FRMA&gt;</c>).
/// </summary>
public class CafNode
{
    [XmlAttribute("version")]
    public string Version { get; set; } = "1.0";

    [XmlElement("DA")]
    public CafAuthorizationData DatosAutorizacion { get; set; } = new();

    [XmlElement("FRMA")]
    public CafSignatureNode FirmaSii { get; set; } = new();
}

/// <summary>
/// Nodo <c>&lt;DA&gt;</c> con los datos del contribuyente y el rango de folios autorizados por el SII.
/// </summary>
public class CafAuthorizationData
{
    [XmlElement("RE")]
    public string RutEmisor { get; set; } = string.Empty;

    [XmlElement("RS")]
    public string RazonSocial { get; set; } = string.Empty;

    [XmlElement("TD")]
    public int TipoDte { get; set; }

    [XmlElement("RNG")]
    public CafFolioRange RangoFolios { get; set; } = new();

    [XmlElement("FA")]
    public string FechaAutorizacion { get; set; } = string.Empty;

    [XmlElement("RSAPK")]
    public CafRsaPublicKey? ClavePublicaRsa { get; set; }

    [XmlElement("IDK")]
    public long? IdKey { get; set; }
}

/// <summary>
/// Nodo <c>&lt;RNG&gt;</c> con el folio inicial (<c>&lt;D&gt;</c>) y final (<c>&lt;H&gt;</c>) autorizados.
/// </summary>
public class CafFolioRange
{
    [XmlElement("D")]
    public int Desde { get; set; }

    [XmlElement("H")]
    public int Hasta { get; set; }
}

/// <summary>
/// Nodo <c>&lt;RSAPK&gt;</c> con el módulo y exponente público entregados en el CAF.
/// </summary>
public class CafRsaPublicKey
{
    [XmlElement("M")]
    public string Modulo { get; set; } = string.Empty;

    [XmlElement("E")]
    public string Exponente { get; set; } = string.Empty;
}

/// <summary>
/// Nodo <c>&lt;FRMA&gt;</c> con la firma electrónica del CAF o TED.
/// </summary>
public class CafSignatureNode
{
    [XmlAttribute("algoritmo")]
    public string Algoritmo { get; set; } = "SHA1withRSA";

    [XmlText]
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Nodo <c>&lt;RSAPrivKey&gt;</c> con los parámetros criptográficos de la clave privada RSA codificados en Base64.
/// </summary>
public class RsaPrivateKeyNode
{
    [XmlElement("Modulus")]
    public string Modulus { get; set; } = string.Empty;

    [XmlElement("Exponent")]
    public string Exponent { get; set; } = string.Empty;

    [XmlElement("D")]
    public string D { get; set; } = string.Empty;

    [XmlElement("P")]
    public string P { get; set; } = string.Empty;

    [XmlElement("Q")]
    public string Q { get; set; } = string.Empty;

    [XmlElement("DP")]
    public string DP { get; set; } = string.Empty;

    [XmlElement("DQ")]
    public string DQ { get; set; } = string.Empty;

    [XmlElement("InverseQ")]
    public string InverseQ { get; set; } = string.Empty;
}

