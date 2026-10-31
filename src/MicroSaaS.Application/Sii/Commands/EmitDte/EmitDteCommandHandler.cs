using MediatR;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Core.Entities.TenantDomain.Dte;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Sii.Commands.EmitDte;

/// <summary>
/// Manejador de <see cref="EmitDteCommand"/> que ejecuta el flujo completo de emisión de DTE:
/// 1) Recuperación y descifrado del certificado digital del Tenant.
/// 2) Deserialización del archivo CAF (<see cref="CafDocument"/>).
/// 3) Construcción del XML DTE e inyección del TED (<see cref="IDteBuilderService"/>).
/// 4) Firma electrónica XML-DSig con referencia <c>#F{Folio}T{TipoDte}</c> (<see cref="IDocumentSignatureService"/>).
/// 5) Obtención de Token de autenticación en Maullín (<see cref="ISiiAuthenticationService"/>).
/// 6) Envío del DTE firmado al Web Service de Recepción del SII (<see cref="ISiiReceptionService"/>).
/// </summary>
public class EmitDteCommandHandler : IRequestHandler<EmitDteCommand, EmitDteResultDto>
{
    private const string DefaultSiiEnvironment = "maullin";

    private readonly TenantDbContext _tenantDbContext;
    private readonly IEncryptionService _encryptionService;
    private readonly IDteBuilderService _dteBuilderService;
    private readonly IDocumentSignatureService _signatureService;
    private readonly ISiiAuthenticationService _authService;
    private readonly ISiiReceptionService _receptionService;

    public EmitDteCommandHandler(
        TenantDbContext tenantDbContext,
        IEncryptionService encryptionService,
        IDteBuilderService dteBuilderService,
        IDocumentSignatureService signatureService,
        ISiiAuthenticationService authService,
        ISiiReceptionService receptionService)
    {
        _tenantDbContext = tenantDbContext ?? throw new ArgumentNullException(nameof(tenantDbContext));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _dteBuilderService = dteBuilderService ?? throw new ArgumentNullException(nameof(dteBuilderService));
        _signatureService = signatureService ?? throw new ArgumentNullException(nameof(signatureService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _receptionService = receptionService ?? throw new ArgumentNullException(nameof(receptionService));
    }

    public async Task<EmitDteResultDto> Handle(EmitDteCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Document);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CafXmlContent);

        // a) Recuperar el certificado activo del inquilino desde la BD y descifrar su contraseña
        var certificate = await _tenantDbContext.DigitalCertificates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IsActive, cancellationToken);

        if (certificate is null)
        {
            return new EmitDteResultDto(
                IsSuccess: false,
                Status: "NO_CERTIFICATE",
                TrackId: null,
                Glosa: "No se encontró ningún certificado digital (.pfx) activo en la bóveda del Tenant.",
                Timestamp: null,
                Errors: ["Certificado digital no encontrado."],
                SignedDteXml: null,
                RawSiiResponse: null);
        }

        string? signedDteXml = null;

        try
        {
            var decryptedPassword = _encryptionService.Decrypt(certificate.EncryptedPassword);

            // b) Cargar el CAF mediante CafDocument.FromXml()
            var cafDocument = CafDocument.FromXml(request.CafXmlContent);

            // c) Construir el XML del DTE (que internamente inyectará el TED) usando _dteBuilderService.BuildDteXml()
            var unsignedDteXml = _dteBuilderService.BuildDteXml(request.Document, cafDocument);

            // d) Firmar el documento completo usando _signatureService.SignXmlDocument() con la referencia URI #F{Folio}T{TipoDte}
            var header = request.Document.Header;
            var referenceUri = $"#F{header.Folio}T{header.TipoDte}";

            signedDteXml = _signatureService.SignXmlDocument(
                unsignedDteXml,
                certificate,
                decryptedPassword,
                referenceUri);

            // e) Obtener el token de autenticación para el entorno "maullin" vía _authService.GetTokenAsync()
            var token = await _authService.GetTokenAsync(
                certificate,
                decryptedPassword,
                DefaultSiiEnvironment);

            // f) Enviar el documento firmado al SII usando _receptionService.SendDteAsync()
            var receptionResult = await _receptionService.SendDteAsync(
                signedDteXml,
                token,
                header.Emisor.RutEmisor,
                header.Receptor.RutReceptor,
                DefaultSiiEnvironment);

            // g) Retornar el EmitDteResultDto
            return new EmitDteResultDto(
                IsSuccess: receptionResult.IsSuccess,
                Status: receptionResult.Status,
                TrackId: receptionResult.TrackId,
                Glosa: receptionResult.Glosa,
                Timestamp: receptionResult.Timestamp,
                Errors: receptionResult.Errors,
                SignedDteXml: signedDteXml,
                RawSiiResponse: receptionResult.RawXmlResponse);
        }
        catch (Exception ex)
        {
            return new EmitDteResultDto(
                IsSuccess: false,
                Status: "EXCEPTION",
                TrackId: null,
                Glosa: ex.Message,
                Timestamp: null,
                Errors: [ex.Message],
                SignedDteXml: signedDteXml,
                RawSiiResponse: null);
        }
    }
}

