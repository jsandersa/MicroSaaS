using System.Net;
using System.Security;
using System.Text;
using System.Xml.Linq;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Infrastructure.Sii;

/// <summary>
/// Implementación del flujo oficial de autenticación del SII (Semilla -> Firma XML-DSig -> Token)
/// para los ambientes de certificación (<c>maullin</c>) y producción (<c>palena</c>).
/// </summary>
public class SiiAuthenticationService : ISiiAuthenticationService
{
    public const string HttpClientName = "SiiWebServices";

    private readonly IDocumentSignatureService _signatureService;
    private readonly IHttpClientFactory _httpClientFactory;

    public SiiAuthenticationService(
        IDocumentSignatureService signatureService,
        IHttpClientFactory httpClientFactory)
    {
        _signatureService = signatureService ?? throw new ArgumentNullException(nameof(signatureService));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public async Task<string> GetTokenAsync(
        TenantDigitalCertificate certificate,
        string plainTextPassword,
        string environment = "maullin")
    {
        ArgumentNullException.ThrowIfNull(certificate);
        ArgumentException.ThrowIfNullOrWhiteSpace(plainTextPassword);

        var subdomain = string.Equals(environment, "palena", StringComparison.OrdinalIgnoreCase)
            ? "palena"
            : "maullin";

        var seedEndpoint = $"https://{subdomain}.sii.cl/DTEWS/CrSeed.jws?WSDL";
        var tokenEndpoint = $"https://{subdomain}.sii.cl/DTEWS/GetTokenFromSeed.jws?WSDL";

        using var httpClient = _httpClientFactory.CreateClient(HttpClientName);

        // =====================================================================
        // Paso 1: Pedir Semilla al SII
        // =====================================================================
        var seedRawResponse = await RequestSeedAsync(httpClient, seedEndpoint);
        var seedValue = ExtractXmlElementValue(seedRawResponse, "SEMILLA");

        if (string.IsNullOrWhiteSpace(seedValue))
        {
            throw new InvalidOperationException("No fue posible extraer el tag <SEMILLA> desde la respuesta del SII.");
        }

        // =====================================================================
        // Paso 2: Construir XML <getToken> con la semilla y firmarlo digitalmente
        // =====================================================================
        var unsignedSeedXml = $"<getToken><item><Semilla>{SecurityElement.Escape(seedValue.Trim())}</Semilla></item></getToken>";

        var signedSeedXml = _signatureService.SignXmlDocument(
            unsignedSeedXml,
            certificate,
            plainTextPassword);

        // =====================================================================
        // Paso 3: Enviar la semilla firmada mediante HTTP POST para obtener el Token
        // =====================================================================
        var soapTokenRequest = BuildGetTokenSoapEnvelope(signedSeedXml);
        using var tokenContent = new StringContent(soapTokenRequest, Encoding.UTF8, "text/xml");
        tokenContent.Headers.Add("SOAPAction", "\"\"");

        using var tokenResponse = await httpClient.PostAsync(tokenEndpoint, tokenContent);
        tokenResponse.EnsureSuccessStatusCode();

        var tokenRawResponse = await tokenResponse.Content.ReadAsStringAsync();

        // =====================================================================
        // Paso 4: Parsear y retornar el valor del tag <TOKEN>
        // =====================================================================
        var token = ExtractXmlElementValue(tokenRawResponse, "TOKEN");

        if (string.IsNullOrWhiteSpace(token))
        {
            var estado = ExtractXmlElementValue(tokenRawResponse, "ESTADO") ?? "DESCONOCIDO";
            var glosa = ExtractXmlElementValue(tokenRawResponse, "GLOSA") ?? "Sin detalle";
            throw new InvalidOperationException(
                $"El SII no retornó un <TOKEN> válido. Estado: {estado} - Glosa: {glosa}.");
        }

        return token.Trim();
    }

    private static async Task<string> RequestSeedAsync(HttpClient httpClient, string seedEndpoint)
    {
        // Intentar primero HTTP GET según especificación y, si devuelve definición WSDL sin <SEMILLA>, invocar operación getSeed
        var getResponse = await httpClient.GetStringAsync(seedEndpoint);
        if (getResponse.Contains("SEMILLA", StringComparison.OrdinalIgnoreCase))
        {
            return getResponse;
        }

        const string getSeedSoapEnvelope = """
            <soapenv:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                              xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                              xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
                              xmlns:def="http://DefaultNamespace">
               <soapenv:Header/>
               <soapenv:Body>
                  <def:getSeed soapenv:encodingStyle="http://schemas.xmlsoap.org/soap/encoding/"/>
               </soapenv:Body>
            </soapenv:Envelope>
            """;

        using var seedContent = new StringContent(getSeedSoapEnvelope, Encoding.UTF8, "text/xml");
        seedContent.Headers.Add("SOAPAction", "\"\"");

        using var postResponse = await httpClient.PostAsync(seedEndpoint, seedContent);
        postResponse.EnsureSuccessStatusCode();

        return await postResponse.Content.ReadAsStringAsync();
    }

    private static string BuildGetTokenSoapEnvelope(string signedSeedXml)
    {
        return $"""
            <soapenv:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                              xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                              xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
                              xmlns:def="http://DefaultNamespace">
               <soapenv:Header/>
               <soapenv:Body>
                  <def:getToken soapenv:encodingStyle="http://schemas.xmlsoap.org/soap/encoding/">
                     <pszXml xsi:type="xsd:string"><![CDATA[{signedSeedXml}]]></pszXml>
                  </def:getToken>
               </soapenv:Body>
            </soapenv:Envelope>
            """;
    }

    private static string? ExtractXmlElementValue(string rawXmlResponse, string elementName)
    {
        if (string.IsNullOrWhiteSpace(rawXmlResponse))
        {
            return null;
        }

        // Decodificar entidades HTML en caso de que el servicio JWS del SII retorne el XML escapado dentro del body SOAP
        var decodedXml = WebUtility.HtmlDecode(rawXmlResponse);

        try
        {
            var xdoc = XDocument.Parse(decodedXml);
            var element = xdoc
                .Descendants()
                .FirstOrDefault(e => string.Equals(e.Name.LocalName, elementName, StringComparison.OrdinalIgnoreCase));

            return element?.Value;
        }
        catch
        {
            // Fallback por búsqueda directa de tags en caso de múltiples declaraciones XML anidadas tras HtmlDecode
            var openTag = $"<{elementName}>";
            var closeTag = $"</{elementName}>";

            var startIdx = decodedXml.IndexOf(openTag, StringComparison.OrdinalIgnoreCase);
            var endIdx = decodedXml.IndexOf(closeTag, StringComparison.OrdinalIgnoreCase);

            if (startIdx >= 0 && endIdx > startIdx)
            {
                startIdx += openTag.Length;
                return decodedXml[startIdx..endIdx].Trim();
            }

            return null;
        }
    }
}

