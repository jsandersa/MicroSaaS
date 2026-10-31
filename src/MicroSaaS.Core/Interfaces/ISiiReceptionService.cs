namespace MicroSaaS.Core.Interfaces;

/// <summary>
/// Resultado del envío de un Documento Tributario Electrónico (DTE) al Web Service de Recepción del SII.
/// </summary>
public record SiiReceptionResultDto(
    bool IsSuccess,
    string Status,
    string? TrackId,
    string Glosa,
    string? Timestamp,
    IReadOnlyList<string> Errors,
    string RawXmlResponse);

/// <summary>
/// Contrato para el servicio de transmisión y recepción de DTEs ante el SII (<c>DTEUpload</c>).
/// </summary>
public interface ISiiReceptionService
{
    Task<SiiReceptionResultDto> SendDteAsync(
        string signedDteXml,
        string token,
        string rutEmisor,
        string rutReceptor,
        string environment = "maullin");
}

