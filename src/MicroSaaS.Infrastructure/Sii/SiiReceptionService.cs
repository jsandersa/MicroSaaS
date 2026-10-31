using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Infrastructure.Sii;

/// <summary>
/// Cliente HTTP encargado de transmitir Documentos Tributarios Electrónicos (DTE) firmados
/// hacia el endpoint CGI de recepción del SII (<c>/cgi_dte/UPL/DTEUpload</c>) mediante <c>multipart/form-data</c>.
/// </summary>
public class SiiReceptionService : ISiiReceptionService
{
    private const string SiiUserAgent = "Mozilla/4.0 ( compatible; PROG 1.0; Windows NT 5.0; YComp 5.0.2.4)";
    private readonly IHttpClientFactory _httpClientFactory;

    public SiiReceptionService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public async Task<SiiReceptionResultDto> SendDteAsync(
        string signedDteXml,
        string token,
        string rutEmisor,
        string rutReceptor,
        string environment = "maullin")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signedDteXml);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(rutEmisor);
        ArgumentException.ThrowIfNullOrWhiteSpace(rutReceptor);

        var subdomain = string.Equals(environment, "palena", StringComparison.OrdinalIgnoreCase)
            ? "palena"
            : "maullin";

        var uploadEndpoint = $"https://{subdomain}.sii.cl/cgi_dte/UPL/DTEUpload";

        var (rutSenderBody, dvSender) = SplitRut(rutEmisor);
        var (rutCompanyBody, dvCompany) = SplitRut(rutEmisor);

        using var httpClient = _httpClientFactory.CreateClient(SiiAuthenticationService.HttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Post, uploadEndpoint);
        request.Headers.TryAddWithoutValidation("User-Agent", SiiUserAgent);
        request.Headers.TryAddWithoutValidation("Cookie", $"TOKEN={token.Trim()}");
        request.Headers.TryAddWithoutValidation("Referer", $"https://{subdomain}.sii.cl");

        // Construir el contenido multipart/form-data exigido por el CGI DTEUpload del SII
        var boundary = $"------------------------{DateTime.UtcNow.Ticks:x}";
        using var multipartContent = new MultipartFormDataContent(boundary);

        multipartContent.Add(CreateFormField(rutSenderBody), "\"rutSender\"");
        multipartContent.Add(CreateFormField(dvSender), "\"dvSender\"");
        multipartContent.Add(CreateFormField(rutCompanyBody), "\"rutCompany\"");
        multipartContent.Add(CreateFormField(dvCompany), "\"dvCompany\"");

        var xmlBytes = Encoding.Latin1.GetBytes(signedDteXml);
        var fileContent = new ByteArrayContent(xmlBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/xml")
        {
            CharSet = "ISO-8859-1"
        };

        multipartContent.Add(fileContent, "\"archivo\"", $"\"dte_{rutCompanyBody}_{DateTime.UtcNow:yyyyMMddHHmmss}.xml\"");

        request.Content = multipartContent;

        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var rawXmlResponse = await response.Content.ReadAsStringAsync();

        return ParseSiiUploadResponse(rawXmlResponse);
    }

    private static StringContent CreateFormField(string value)
    {
        return new StringContent(value, Encoding.Latin1);
    }

    private static (string Body, string Dv) SplitRut(string rut)
    {
        var normalized = rut
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();

        var parts = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            return (parts[0].Trim(), parts[1].Trim());
        }

        if (normalized.Length >= 2)
        {
            return (normalized[..^1], normalized[^1..]);
        }

        throw new ArgumentException($"El RUT '{rut}' no tiene un formato chileno válido (ej. 77654321-K).", nameof(rut));
    }

    private static SiiReceptionResultDto ParseSiiUploadResponse(string rawXmlResponse)
    {
        if (string.IsNullOrWhiteSpace(rawXmlResponse))
        {
            return new SiiReceptionResultDto(
                IsSuccess: false,
                Status: "-1",
                TrackId: null,
                Glosa: "El SII retornó una respuesta vacía.",
                Timestamp: null,
                Errors: ["Respuesta vacía desde DTEUpload."],
                RawXmlResponse: string.Empty);
        }

        try
        {
            var xdoc = XDocument.Parse(rawXmlResponse);

            var status = GetElementValue(xdoc, "STATUS") ?? "-1";
            var trackId = GetElementValue(xdoc, "TRACKID");
            var timestamp = GetElementValue(xdoc, "TIMESTAMP");

            var errors = xdoc
                .Descendants()
                .Where(e => string.Equals(e.Name.LocalName, "ERROR", StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Value.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();

            var isSuccess = string.Equals(status, "0", StringComparison.Ordinal) &&
                            !string.IsNullOrWhiteSpace(trackId);

            var statusGlosa = ResolveStatusGlosa(status, errors);

            return new SiiReceptionResultDto(
                IsSuccess: isSuccess,
                Status: status,
                TrackId: trackId,
                Glosa: statusGlosa,
                Timestamp: timestamp,
                Errors: errors,
                RawXmlResponse: rawXmlResponse);
        }
        catch (Exception ex)
        {
            return new SiiReceptionResultDto(
                IsSuccess: false,
                Status: "PARSE_ERROR",
                TrackId: null,
                Glosa: $"No fue posible parsear el XML de recepción del SII: {ex.Message}",
                Timestamp: null,
                Errors: [ex.Message],
                RawXmlResponse: rawXmlResponse);
        }
    }

    private static string? GetElementValue(XDocument xdoc, string localName)
    {
        return xdoc
            .Descendants()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))
            ?.Value
            .Trim();
    }

    private static string ResolveStatusGlosa(string status, IReadOnlyList<string> errors)
    {
        var baseDescription = status switch
        {
            "0" => "Envío recibido correctamente por el SII (Upload OK).",
            "1" => "Rechazado: El Sender no tiene permiso para enviar documentos.",
            "2" => "Rechazado: Error en el tamaño del archivo enviado.",
            "3" => "Rechazado: Archivo cortado (tamaño distinto al esperado).",
            "5" => "Rechazado: No está autenticado (Token inválido o expirado).",
            "6" => "Rechazado: Empresa no autorizada por el SII a enviar archivos DTE.",
            "7" => "Rechazado: Esquema XML inválido.",
            "8" => "Rechazado: Error en la firma digital del documento.",
            "9" => "Rechazado: Sistema bloqueado temporalmente en el SII.",
            _ => $"Estado de recepción SII código '{status}'."
        };

        if (errors.Count > 0)
        {
            return $"{baseDescription} Detalle: {string.Join(" | ", errors)}";
        }

        return baseDescription;
    }
}

