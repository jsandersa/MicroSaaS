using MediatR;

namespace MicroSaaS.Application.Sii.Queries.TestSiiConnection;

public record SiiConnectionResultDto(
    bool IsSuccess,
    string? Token,
    string? ErrorMessage,
    string Environment,
    string? CertificateSubject);

/// <summary>
/// Consulta MediatR para probar la autenticación real contra el entorno de certificación del SII (Maullín)
/// utilizando el certificado digital activo del Tenant actual.
/// </summary>
public record TestSiiConnectionQuery : IRequest<SiiConnectionResultDto>;

