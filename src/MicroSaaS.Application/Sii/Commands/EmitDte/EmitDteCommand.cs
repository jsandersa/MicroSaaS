using MediatR;
using MicroSaaS.Core.Entities.TenantDomain.Dte;

namespace MicroSaaS.Application.Sii.Commands.EmitDte;

/// <summary>
/// Comando para orquestar la generación del TED, construcción del XML DTE, firma digital XML-DSig,
/// autenticación con el SII y envío del documento electrónico a Maullín.
/// </summary>
public record EmitDteCommand(
    Guid TenantId,
    DteDocument Document,
    string CafXmlContent) : IRequest<EmitDteResultDto>;

/// <summary>
/// DTO de respuesta del flujo completo de emisión y recepción de un DTE ante el SII.
/// </summary>
public record EmitDteResultDto(
    bool IsSuccess,
    string Status,
    string? TrackId,
    string Glosa,
    string? Timestamp,
    IReadOnlyList<string> Errors,
    string? SignedDteXml,
    string? RawSiiResponse);

