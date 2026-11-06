using MediatR;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Application.ElectronicInvoicing.Commands.EmitDte;

/// <summary>
/// Manejador de <see cref="EmitDteCommand"/> que obtiene el siguiente folio correlativo del Tenant,
/// instancia el <see cref="DteDocument"/>, delega la firma/emisión en <see cref="IDteEmissionService"/> y persiste el DTE.
/// </summary>
public sealed class EmitDteCommandHandler : IRequestHandler<EmitDteCommand, Guid>
{
    private readonly IDteRepository _dteRepository;
    private readonly IDteEmissionService _dteEmissionService;
    private readonly ICurrentUserService _currentUserService;

    public EmitDteCommandHandler(
        IDteRepository dteRepository,
        IDteEmissionService dteEmissionService,
        ICurrentUserService currentUserService)
    {
        _dteRepository = dteRepository ?? throw new ArgumentNullException(nameof(dteRepository));
        _dteEmissionService = dteEmissionService ?? throw new ArgumentNullException(nameof(dteEmissionService));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<Guid> Handle(
        EmitDteCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // a. Obtener el TenantId desde ICurrentUserService
        var tenantId = _currentUserService.TenantId;
        if (tenantId == Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "No se pudo determinar el TenantId del usuario autenticado desde el token JWT.");
        }

        // b. Obtener el siguiente correlativo llamando a GetNextFolioAsync en el repositorio
        var nextFolio = await _dteRepository.GetNextFolioAsync(
            tenantId: tenantId,
            dteType: request.DteType,
            cancellationToken: cancellationToken);

        // c. Crear la entidad DteDocument
        var dteDocument = DteDocument.Create(
            tenantId: tenantId,
            saleId: request.SaleId,
            dteType: request.DteType,
            folio: nextFolio,
            totalAmount: request.TotalAmount);

        // d. Llamar a IDteEmissionService.SignAndEmitAsync (pasándole el documento)
        var signedXmlContent = await _dteEmissionService.SignAndEmitAsync(
            document: dteDocument,
            cancellationToken: cancellationToken);

        // e. Actualizar el estado del documento y persistir en IDteRepository
        dteDocument.MarkAsSigned(signedXmlContent);
        dteDocument.MarkAsSentToSii();
        dteDocument.MarkAsAccepted();

        await _dteRepository.AddAsync(dteDocument, cancellationToken);

        return dteDocument.Id;
    }
}

